namespace Nalu.Internals;

using System.Text;

/// <summary>
/// Provides extension methods for the <see cref="string"/> class.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string to Base64 RFC4648-5-FP.
    /// </summary>
    /// <remarks>
    /// Base64 RFC4648-5 filename safe is a Base64 encoding where padding is stored in the last character as '0' / '1' / '2'.
    /// </remarks>
    /// <param name="value">The string to be encoded.</param>
    public static string ToBase64FilenameSafe(this string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = Encoding.UTF8.GetBytes(value);
        var encoded = Convert.ToBase64String(bytes);

        // Get padding count
        var padding = 0;
        var encodedLength = encoded.Length;
        if (encodedLength > 0 && encoded[encodedLength - 1] == '=')
        {
            ++padding;
        }

        if (encodedLength > 1 && encoded[encodedLength - 2] == '=')
        {
            ++padding;
        }

        encodedLength -= padding;
        var result = string.Create(encodedLength + 1, encoded, (chars, state) =>
        {
            for (var i = 0; i < encodedLength; ++i)
            {
                var c = state[i];
                chars[i] = c switch
                {
                    '/' => '_',
                    '+' => '-',
                    _ => c,
                };
            }

            chars[encodedLength] = padding switch
            {
                1 => '1',
                2 => '2',
                _ => '0',
            };
        });

        return result;
    }

    /// <summary>
    /// Converts a string from Base64 RFC4648-5-FP.
    /// </summary>
    /// <remarks>
    /// Base64 RFC4648-5 filename safe is a Base64 encoding where padding is stored in the last character as '0' / '1' / '2'.
    /// </remarks>
    /// <param name="value">The encoded string.</param>
    public static string FromBase64FilenameSafe(this string value)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);

        var padding = value[^1] switch
        {
            '1' => 1,
            '2' => 2,
            _ => 0,
        };

        var encodedLength = value.Length - 1 + padding;
        var encoded = string.Create(encodedLength, value, (chars, state) =>
        {
            if (padding > 0)
            {
                chars[encodedLength - 1] = '=';
            }

            if (padding > 1)
            {
                chars[encodedLength - 2] = '=';
            }

            var stateLength = state.Length - 1;
            for (var i = 0; i < stateLength; ++i)
            {
                var c = state[i];
                chars[i - 1] = c switch
                {
                    '_' => '/',
                    '-' => '+',
                    _ => c,
                };
            }
        });

        var bytes = Convert.FromBase64String(encoded);
        return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
    }
}
