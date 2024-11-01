// ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
namespace Nalu;

using System.Globalization;
using System.Text;
using Foundation;

// useful extensions for the class in order to set it in a header
internal static class NsHttpCookieExtensions
{
    // returns the header for a cookie
    public static string GetHeaderValue(this NSHttpCookie cookie)
    {
        var header = new StringBuilder();
        AppendSegment(header, cookie.Name, cookie.Value);
        AppendSegment(header, NSHttpCookie.KeyPath.ToString(), cookie.Path.ToString());
        AppendSegment(header, NSHttpCookie.KeyDomain.ToString(), cookie.Domain.ToString());
        AppendSegment(header, NSHttpCookie.KeyVersion.ToString(), cookie.Version.ToString(CultureInfo.InvariantCulture));

        if (cookie.Comment is not null)
        {
            AppendSegment(header, NSHttpCookie.KeyComment.ToString(), cookie.Comment.ToString());
        }

        if (cookie.CommentUrl is not null)
        {
            AppendSegment(header, NSHttpCookie.KeyCommentUrl.ToString(), cookie.CommentUrl.ToString());
        }

        if (cookie.Properties.ContainsKey(NSHttpCookie.KeyDiscard))
        {
            AppendSegment(header, NSHttpCookie.KeyDiscard.ToString(), null);
        }

        if (cookie.ExpiresDate is not null)
        {
            // Format according to RFC1123; 'r' uses invariant info(DateTimeFormatInfo.InvariantInfo)
            var dateStr = ((DateTime)cookie.ExpiresDate).ToUniversalTime().ToString("r", CultureInfo.InvariantCulture);
            AppendSegment(header, NSHttpCookie.KeyExpires.ToString(), dateStr);
        }

        if (cookie.Properties.ContainsKey(NSHttpCookie.KeyMaximumAge))
        {
            var timeStampString = (NSString)cookie.Properties[NSHttpCookie.KeyMaximumAge];
            AppendSegment(header, NSHttpCookie.KeyMaximumAge.ToString(), timeStampString);
        }

        if (cookie.IsSecure)
        {
            AppendSegment(header, NSHttpCookie.KeySecure.ToString(), null);
        }

        if (cookie.IsHttpOnly)
        {
            AppendSegment(header, "httponly", null); // Apple does not show the key for the httponly
        }

        return header.ToString();
    }

    private static void AppendSegment(StringBuilder builder, string name, string? value)
    {
        if (builder.Length > 0)
        {
            builder.Append("; ");
        }

        builder.Append(name);
        if (value is not null)
        {
            builder.Append('=').Append(value);
        }
    }
}
