using System.Net;
using Foundation;

namespace Nalu;

internal static class NSUrlSessionTaskExtensions
{
    public static bool IsCanceled(this NSUrlSessionTask task)
        => task.State == NSUrlSessionTaskState.Canceling || (task.Error?.Code ?? 0) == -999;

    public static HttpStatusCode GetHttpStatusCode(this NSUrlSessionTask task)
        => (HttpStatusCode) ((task.Response as NSHttpUrlResponse)?.StatusCode ?? 0);
}
