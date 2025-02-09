using CommunityToolkit.Mvvm.ComponentModel;

namespace Nalu.Maui.Sample.PageModels;

public class SevenPageModel : ObservableObject
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
}
