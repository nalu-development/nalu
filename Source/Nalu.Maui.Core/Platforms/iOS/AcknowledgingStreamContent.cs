namespace Nalu;

internal class AcknowledgingStreamContent(MessageHandlerNSUrlSessionDownloadDelegate messageHandler, NSUrlRequestHandle handle, Stream stream) : StreamContent(stream)
{
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            messageHandler.CompleteAndRemoveHandle(handle);
        }
    }
}
