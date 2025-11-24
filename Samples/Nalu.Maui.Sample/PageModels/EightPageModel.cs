using CommunityToolkit.Mvvm.ComponentModel;

namespace Nalu.Maui.Sample.PageModels;

public partial class EightPageModel : ObservableObject
{
    private static int _instanceCount;

    public string Message { get; } = "This is page eight - testing 5+ tabs!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
}

