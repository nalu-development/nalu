using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Nalu.Chaos;

/// <summary>One HTTP request the chaos server received, as parsed off the wire.</summary>
public sealed record ChaosRequest(
    DateTimeOffset Timestamp,
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    long BodyLength,
    string BodySha256
);

/// <summary>
/// A RAW-SOCKET HTTP server whose per-request behavior is selected by the request PATH, built to
/// produce network faults real HTTP servers refuse to produce: truncated bodies, mid-body RST,
/// non-HTTP garbage, byte-drips, stalls, redirect loops. Drives the error-handling test matrix of
/// <c>NSUrlBackgroundSessionHttpMessageHandler</c> (see the "Background Http Chaos" TestApp page
/// and UITests.DevFlow's BackgroundHttpChaosUiTests).
/// </summary>
/// <remarks>
/// Raw sockets on purpose: Kestrel/HttpListener validate what they send, and the whole point here
/// is sending protocol violations. One request per connection (<c>Connection: close</c>) keeps the
/// parsing honest and matches how NSUrlSession treats faulted connections anyway.
///
/// Behaviors (query values are integers):
/// <list type="bullet">
/// <item><c>/ok[?bytes=N&amp;status=S]</c> — plain response (default 200, small JSON body).</item>
/// <item><c>/status/NNN</c> — the given HTTP status with a small body.</item>
/// <item><c>/echo</c> — 200 JSON describing the received request (method, body length/sha, cookie header).</item>
/// <item><c>/delay?ms=N</c> — full response after N ms.</item>
/// <item><c>/stall[?ms=N]</c> — reads the request then sends NOTHING for N ms (default 10 min).</item>
/// <item><c>/truncate?declared=D&amp;send=S</c> — Content-Length D, sends only S bytes, clean close.</item>
/// <item><c>/reset[?after=N]</c> — abortive close (RST); with N&gt;0, after status/headers + N body bytes.</item>
/// <item><c>/garbage</c> — non-HTTP bytes, then close.</item>
/// <item><c>/drip?bytes=N&amp;delayms=D&amp;chunk=C</c> — 200 with N bytes written C at a time every D ms.</item>
/// <item><c>/huge?mb=N</c> — N megabytes of deterministic bytes.</item>
/// <item><c>/redirect?n=N</c> — a chain of N 302s landing on 200.</item>
/// <item><c>/redirect-loop</c> — 302 to itself, forever.</item>
/// <item><c>/cookies</c> — two Set-Cookie headers; body echoes the Cookie header it received.</item>
/// </list>
/// </remarks>
public sealed class ChaosServer : IAsyncDisposable
{
    private static readonly byte[] _bodyPattern = "0123456789"u8.ToArray();

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private readonly ConcurrentQueue<ChaosRequest> _requests = new();

    /// <summary>The port the server actually bound (an ephemeral one when constructed with 0).</summary>
    public int Port { get; }

    /// <summary>Raised for every parsed request (diagnostics/logging).</summary>
    public event Action<ChaosRequest>? RequestReceived;

    /// <summary>Every request received since construction or the last <see cref="ClearRequests" />.</summary>
    public IReadOnlyList<ChaosRequest> Requests => [.. _requests];

    public ChaosServer(int port = 0)
    {
        _listener = new TcpListener(IPAddress.Any, port);
        _listener.Start();
        Port = ((IPEndPoint) _listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public void ClearRequests() => _requests.Clear();

    /// <summary>Waits until a received request satisfies the predicate (including past ones).</summary>
    public async Task<ChaosRequest> WaitForRequestAsync(Func<ChaosRequest, bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (_requests.FirstOrDefault(predicate) is { } match)
            {
                return match;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        throw new TimeoutException(
            $"No request matched within {timeout.TotalSeconds:0.#}s. Received: [{string.Join(", ", _requests.Select(r => $"{r.Method} {r.Path}"))}]");
    }

    /// <summary>
    /// The machine's LAN IPv4 (for devices on the same Wi-Fi), preferring private ranges and
    /// skipping link-local/loopback. Null when the machine has no usable IPv4.
    /// </summary>
    public static IPAddress? GetLanAddress()
        => NetworkInterface
           .GetAllNetworkInterfaces()
           .Where(i => i.OperationalStatus == OperationalStatus.Up
                       && i.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
           .SelectMany(i => i.GetIPProperties().UnicastAddresses)
           .Select(a => a.Address)
           .Where(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
           .OrderBy(a => IsPrivate(a) ? 0 : 1)
           .ThenBy(a => a.ToString().StartsWith("169.254", StringComparison.Ordinal) ? 1 : 0)
           .FirstOrDefault();

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();

        return bytes[0] == 10
               || (bytes[0] == 192 && bytes[1] == 168)
               || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31);
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                continue;
            }

            _ = Task.Run(() => HandleClientSafeAsync(client));
        }
    }

    private async Task HandleClientSafeAsync(TcpClient client)
    {
        try
        {
            await HandleClientAsync(client).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Faults are this server's job: a peer bailing mid-scenario is expected.
        }
        finally
        {
            try
            {
                client.Dispose();
            }
            catch (Exception)
            {
                // An abortive close may already have torn the socket down.
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        client.NoDelay = true;
        var stream = client.GetStream();
        var ct = _cts.Token;

        var (method, target, headers) = await ReadRequestHeadAsync(stream, ct).ConfigureAwait(false);

        var pathAndQuery = target;
        var queryIndex = pathAndQuery.IndexOf('?');
        var path = queryIndex < 0 ? pathAndQuery : pathAndQuery[..queryIndex];
        var query = ParseQuery(queryIndex < 0 ? string.Empty : pathAndQuery[(queryIndex + 1)..]);

        int Q(string name, int fallback)
            => query.TryGetValue(name, out var raw) && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;

        // /slow-read drains the request body deliberately slowly, so a client can be killed
        // MID-UPLOAD deterministically (chunk bytes every delayms).
        var (throttleChunk, throttleDelayMs) = path == "/slow-read"
            ? (Math.Max(1, Q("chunk", 16 * 1024)), Math.Max(1, Q("delayms", 100)))
            : (0, 0);

        var (bodyLength, bodySha) = await ReadRequestBodyAsync(stream, headers, throttleChunk, throttleDelayMs, ct).ConfigureAwait(false);

        var request = new ChaosRequest(DateTimeOffset.Now, method, pathAndQuery, headers, bodyLength, bodySha);
        _requests.Enqueue(request);
        RequestReceived?.Invoke(request);

        switch (path)
        {
            case "/ok":
            {
                var bytes = Q("bytes", -1);
                var body = bytes < 0 ? Encoding.UTF8.GetBytes("""{"ok":true}""") : PatternBody(bytes);
                await WriteResponseAsync(stream, Q("status", 200), body, [("Content-Type", bytes < 0 ? "application/json" : "application/octet-stream")], ct: ct).ConfigureAwait(false);

                break;
            }

            case var _ when path.StartsWith("/status/", StringComparison.Ordinal)
                            && int.TryParse(path["/status/".Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var statusCode):
            {
                await WriteResponseAsync(stream, statusCode, Encoding.UTF8.GetBytes($$"""{"status":{{statusCode}}}"""), [("Content-Type", "application/json")], ct: ct).ConfigureAwait(false);

                break;
            }

            case "/echo":
            {
                if (Q("delayms", 0) is > 0 and var echoDelay)
                {
                    await Task.Delay(echoDelay, ct).ConfigureAwait(false);
                }

                var cookie = headers.TryGetValue("cookie", out var cookieHeader) ? cookieHeader : string.Empty;

                var body = Encoding.UTF8.GetBytes(FormattableString.Invariant(
                    $$"""{"method":"{{method}}","bodyLength":{{bodyLength}},"bodySha256":"{{bodySha}}","cookie":"{{cookie.Replace("\"", "'")}}"}"""));

                await WriteResponseAsync(stream, 200, body, [("Content-Type", "application/json")], ct: ct).ConfigureAwait(false);

                break;
            }

            case "/delay":
            {
                await Task.Delay(Q("ms", 2000), ct).ConfigureAwait(false);
                await WriteResponseAsync(stream, 200, Encoding.UTF8.GetBytes("""{"delayed":true}"""), [("Content-Type", "application/json")], ct: ct).ConfigureAwait(false);

                break;
            }

            case "/stall":
            {
                // Silence: no status line, no bytes. The peer sees an open connection that
                // never answers — the shape of a native request timeout.
                await Task.Delay(Q("ms", 600_000), ct).ConfigureAwait(false);
                await WriteResponseAsync(stream, 200, Encoding.UTF8.GetBytes("""{"stalled":true}"""), [("Content-Type", "application/json")], ct: ct).ConfigureAwait(false);

                break;
            }

            case "/truncate":
            {
                var declared = Q("declared", 10_000);
                var send = Math.Min(Q("send", 1_000), declared);
                await WriteResponseAsync(stream, 200, PatternBody(send), [("Content-Type", "application/octet-stream")], declaredLength: declared, ct: ct).ConfigureAwait(false);

                // Clean FIN with fewer bytes than Content-Length promised.
                break;
            }

            case "/reset":
            {
                var after = Q("after", 0);

                if (after > 0)
                {
                    await WriteResponseAsync(stream, 200, PatternBody(after), [("Content-Type", "application/octet-stream")], declaredLength: after + 100_000, ct: ct).ConfigureAwait(false);
                }

                // Abortive close: RST instead of FIN.
                client.Client.LingerState = new LingerOption(true, 0);
                client.Close();

                break;
            }

            case "/garbage":
            {
                var junk = Encoding.ASCII.GetBytes("THIS IS NOT HTTP AT ALL 🤖 chaos chaos chaos\r\n\r\n" + new string('x', 512));
                await stream.WriteAsync(junk, ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);

                break;
            }

            case "/drip":
            {
                var total = Q("bytes", 200);
                var delay = Q("delayms", 50);
                var chunk = Math.Max(1, Q("chunk", 10));
                await WriteHeadAsync(stream, 200, total, [("Content-Type", "application/octet-stream")], ct).ConfigureAwait(false);

                for (var sent = 0; sent < total; sent += chunk)
                {
                    var size = Math.Min(chunk, total - sent);
                    await stream.WriteAsync(PatternBody(size), ct).ConfigureAwait(false);
                    await stream.FlushAsync(ct).ConfigureAwait(false);
                    await Task.Delay(delay, ct).ConfigureAwait(false);
                }

                break;
            }

            case "/huge":
            {
                var megabytes = Q("mb", 20);
                var total = (long) megabytes * 1024 * 1024;
                await WriteHeadAsync(stream, 200, total, [("Content-Type", "application/octet-stream")], ct).ConfigureAwait(false);

                var block = PatternBody(64 * 1024);

                for (long sent = 0; sent < total; sent += block.Length)
                {
                    var size = (int) Math.Min(block.Length, total - sent);
                    await stream.WriteAsync(block.AsMemory(0, size), ct).ConfigureAwait(false);
                }

                await stream.FlushAsync(ct).ConfigureAwait(false);

                break;
            }

            case "/redirect":
            {
                var remaining = Q("n", 1);

                if (remaining > 0)
                {
                    await WriteResponseAsync(
                            stream, 302, [],
                            [("Location", FormattableString.Invariant($"/redirect?n={remaining - 1}"))],
                            ct: ct)
                        .ConfigureAwait(false);
                }
                else
                {
                    await WriteResponseAsync(stream, 200, Encoding.UTF8.GetBytes("""{"redirected":true}"""), [("Content-Type", "application/json")], ct: ct).ConfigureAwait(false);
                }

                break;
            }

            case "/redirect-loop":
            {
                // Optional per-hop delay: stretches the ~21-hop chase so the eventual
                // NSURLErrorHTTPTooManyRedirects can be timed against app-lifecycle events.
                if (Q("delayms", 0) is > 0 and var hopDelay)
                {
                    await Task.Delay(hopDelay, ct).ConfigureAwait(false);
                }

                var location = query.TryGetValue("delayms", out var delayValue)
                    ? FormattableString.Invariant($"/redirect-loop?delayms={delayValue}")
                    : "/redirect-loop";

                await WriteResponseAsync(stream, 302, [], [("Location", location)], ct: ct).ConfigureAwait(false);

                break;
            }

            case "/slow-read":
            {
                // Body already drained (throttled) above: acknowledge with what actually arrived.
                var body = Encoding.UTF8.GetBytes(FormattableString.Invariant($$"""{"slowRead":true,"bodyLength":{{bodyLength}}}"""));
                await WriteResponseAsync(stream, 200, body, [("Content-Type", "application/json")], ct: ct).ConfigureAwait(false);

                break;
            }

            case "/chunked":
            {
                // Chunked transfer-encoding: no Content-Length anywhere — exercises the
                // client's unknown-length download path.
                var total = Q("bytes", 1000);
                var head = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(head), ct).ConfigureAwait(false);

                const int chunkSize = 100;

                for (var sent = 0; sent < total; sent += chunkSize)
                {
                    var size = Math.Min(chunkSize, total - sent);
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(FormattableString.Invariant($"{size:x}\r\n")), ct).ConfigureAwait(false);
                    await stream.WriteAsync(PatternBody(size), ct).ConfigureAwait(false);
                    await stream.WriteAsync("\r\n"u8.ToArray(), ct).ConfigureAwait(false);
                }

                await stream.WriteAsync(Encoding.ASCII.GetBytes("0\r\n\r\n"), ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);

                break;
            }

            case "/gzip":
            {
                // Content-Encoding gzip: the on-wire length differs from the decoded one —
                // exercises the client's automatic decompression.
                var total = Q("bytes", 1000);
                using var compressed = new MemoryStream();

                await using (var gzip = new System.IO.Compression.GZipStream(compressed, System.IO.Compression.CompressionLevel.Fastest, true))
                {
                    await gzip.WriteAsync(PatternBody(total), ct).ConfigureAwait(false);
                }

                await WriteResponseAsync(
                        stream, 200, compressed.ToArray(),
                        [("Content-Type", "application/octet-stream"), ("Content-Encoding", "gzip")],
                        ct: ct)
                    .ConfigureAwait(false);

                break;
            }

            case "/cookies":
            {
                var cookie = headers.TryGetValue("cookie", out var cookieHeader) ? cookieHeader : string.Empty;

                var body = Encoding.UTF8.GetBytes(FormattableString.Invariant(
                    $$"""{"receivedCookie":"{{cookie.Replace("\"", "'")}}"}"""));

                await WriteResponseAsync(
                        stream, 200, body,
                        [
                            ("Content-Type", "application/json"),
                            ("Set-Cookie", "chaos1=alpha; Path=/"),
                            ("Set-Cookie", "chaos2=beta; Path=/")
                        ],
                        ct: ct)
                    .ConfigureAwait(false);

                break;
            }

            default:
            {
                await WriteResponseAsync(stream, 404, Encoding.UTF8.GetBytes("""{"error":"unknown chaos path"}"""), [("Content-Type", "application/json")], ct: ct).ConfigureAwait(false);

                break;
            }
        }
    }

    private static async Task<(string Method, string Target, Dictionary<string, string> Headers)> ReadRequestHeadAsync(NetworkStream stream, CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        var length = 0;

        while (true)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(length), ct).ConfigureAwait(false);

            if (read == 0)
            {
                throw new IOException("Connection closed before the request head completed.");
            }

            length += read;

            if (HeadEnd(buffer, length) is { } headEnd)
            {
                var head = Encoding.ASCII.GetString(buffer, 0, headEnd);
                var lines = head.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                var requestLine = lines[0].Split(' ');
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in lines.Skip(1))
                {
                    var colon = line.IndexOf(':');

                    if (colon > 0)
                    {
                        headers[line[..colon].Trim().ToLowerInvariant()] = line[(colon + 1)..].Trim();
                    }
                }

                // Bytes past the head belong to the body: stash them for the body reader.
                var leftover = length - headEnd - 4;
                headers["x-chaos-leftover"] = Convert.ToBase64String(buffer, headEnd + 4, leftover);

                return (requestLine[0], requestLine.Length > 1 ? requestLine[1] : "/", headers);
            }

            if (length == buffer.Length)
            {
                throw new IOException("Request head exceeds 64KB.");
            }
        }

        static int? HeadEnd(byte[] buffer, int length)
        {
            for (var i = 3; i < length; i++)
            {
                if (buffer[i] == '\n' && buffer[i - 1] == '\r' && buffer[i - 2] == '\n' && buffer[i - 3] == '\r')
                {
                    return i - 3;
                }
            }

            return null;
        }
    }

    private static async Task<(long Length, string Sha256)> ReadRequestBodyAsync(
        NetworkStream stream,
        Dictionary<string, string> headers,
        int throttleChunk,
        int throttleDelayMs,
        CancellationToken ct)
    {
        var leftover = Convert.FromBase64String(headers["x-chaos-leftover"]);
        headers.Remove("x-chaos-leftover");

        if (!headers.TryGetValue("content-length", out var lengthHeader)
            || !long.TryParse(lengthHeader, NumberStyles.Integer, CultureInfo.InvariantCulture, out var contentLength)
            || contentLength <= 0)
        {
            return (0, string.Empty);
        }

        using var sha = SHA256.Create();
        long total = 0;

        void Hash(byte[] data, int count)
        {
            sha.TransformBlock(data, 0, count, null, 0);
            total += count;
        }

        Hash(leftover, leftover.Length);

        var buffer = new byte[64 * 1024];
        var readLimit = throttleChunk > 0 ? Math.Min(throttleChunk, buffer.Length) : buffer.Length;

        while (total < contentLength)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(0, (int) Math.Min(readLimit, contentLength - total)), ct).ConfigureAwait(false);

            if (read == 0)
            {
                break;
            }

            Hash(buffer, read);

            if (throttleDelayMs > 0)
            {
                await Task.Delay(throttleDelayMs, ct).ConfigureAwait(false);
            }
        }

        sha.TransformFinalBlock([], 0, 0);

        return (total, Convert.ToHexString(sha.Hash!)[..16].ToLowerInvariant());
    }

    private static byte[] PatternBody(int size)
    {
        var body = new byte[size];

        for (var i = 0; i < size; i++)
        {
            body[i] = _bodyPattern[i % _bodyPattern.Length];
        }

        return body;
    }

    private static async Task WriteHeadAsync(NetworkStream stream, int status, long contentLength, (string Name, string Value)[] headers, CancellationToken ct)
    {
        var builder = new StringBuilder()
                      .Append("HTTP/1.1 ").Append(status.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(ReasonPhrase(status)).Append("\r\n")
                      .Append("Content-Length: ").Append(contentLength.ToString(CultureInfo.InvariantCulture)).Append("\r\n")
                      .Append("Connection: close\r\n");

        foreach (var (name, value) in headers)
        {
            builder.Append(name).Append(": ").Append(value).Append("\r\n");
        }

        builder.Append("\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(builder.ToString()), ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        int status,
        byte[] body,
        (string Name, string Value)[] headers,
        long? declaredLength = null,
        CancellationToken ct = default)
    {
        await WriteHeadAsync(stream, status, declaredLength ?? body.Length, headers, ct).ConfigureAwait(false);

        if (body.Length > 0)
        {
            await stream.WriteAsync(body, ct).ConfigureAwait(false);
            await stream.FlushAsync(ct).ConfigureAwait(false);
        }
    }

    private static string ReasonPhrase(int status)
        => status switch
        {
            200 => "OK",
            302 => "Found",
            404 => "Not Found",
            500 => "Internal Server Error",
            503 => "Service Unavailable",
            _ => "Chaos"
        };

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');

            if (equals > 0)
            {
                values[Uri.UnescapeDataString(pair[..equals])] = Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return values;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _listener.Stop();

        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Shutdown races are fine.
        }

        _cts.Dispose();
    }
}
