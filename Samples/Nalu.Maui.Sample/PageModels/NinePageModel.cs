using CommunityToolkit.Mvvm.ComponentModel;

namespace Nalu.Maui.Sample.PageModels;

public partial class NinePageModel : ObservableObject
{
    private static int _instanceCount;

    public string Message { get; } = "This is page nine - no More tab!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
}

