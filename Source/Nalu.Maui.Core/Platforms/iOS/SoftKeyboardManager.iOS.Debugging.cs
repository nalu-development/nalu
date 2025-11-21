using System.Diagnostics;
using System.Text;
using Foundation;

namespace Nalu;

public static partial class SoftKeyboardManager
{
#if DEBUG
    static partial void DumpInfo(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.WriteLine($"KeyboardNotification: {message}");
    }

    static partial void DumpNotification(NSNotification notification)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"KeyboardNotification: {notification.Name}");

        if (notification.UserInfo is { } userInfo)
        {
            foreach (var (key, value) in userInfo)
            {
                if (value is null)
                {
                    builder.AppendLine($"  {key}: null");
                }
                else if (value is NSString strValue)
                {
                    builder.AppendLine($"  {key}: {strValue}");
                }
                else if (value is NSNumber numValue)
                {
                    builder.AppendLine($"  {key}: {numValue}");
                }
                else
                {
                    builder.AppendLine($"  {key}: {value.GetType().Name} - {value}");
                }
            }
        }

        Debug.WriteLine(builder.ToString());
    }
#endif
}
