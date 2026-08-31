using Nalu.Chaos;

// Standalone runner for manual device sessions:
//   dotnet run --project Tools/Nalu.ChaosServer [port]
// Then point the TestApp's "Background Http Chaos" page at the printed base URL.
var port = args.Length > 0 && int.TryParse(args[0], out var parsed) ? parsed : 9666;

await using var server = new ChaosServer(port);
server.RequestReceived += request => Console.WriteLine($"{request.Timestamp:HH:mm:ss.fff} {request.Method} {request.Path} body={request.BodyLength}");

var lanAddress = ChaosServer.GetLanAddress();
Console.WriteLine($"Chaos server listening on port {server.Port}");
Console.WriteLine($"  device base URL: http://{lanAddress?.ToString() ?? "<this-mac's-LAN-IP>"}:{server.Port}");
Console.WriteLine("  paths: /ok /status/503 /echo /delay?ms= /stall?ms= /truncate /reset /garbage /drip /huge?mb= /redirect?n= /redirect-loop /cookies");
Console.WriteLine("Ctrl+C to stop.");

var exit = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    exit.TrySetResult();
};

await exit.Task;
