using CommunityToolkit.Mvvm.ComponentModel;

namespace Nalu.Maui.Sample.PageModels;

public partial class ElevenPageModel : ObservableObject
{
    private static int _instanceCount;

    public string Message { get; } = "This is page eleven - testing custom tabs!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
}

