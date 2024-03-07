namespace Nalu.Maui.DefaultShellSample.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;

public class FivePageModel : ObservableObject
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
}
