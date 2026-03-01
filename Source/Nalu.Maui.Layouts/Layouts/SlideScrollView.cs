namespace Nalu;

internal class SlideScrollView : ScrollView
{
    public event EventHandler? DraggingEnded;
    
    public void SendDraggingEnded() => DraggingEnded?.Invoke(this, EventArgs.Empty);
}
