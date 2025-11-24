using CommunityToolkit.Mvvm.ComponentModel;

namespace Nalu.Maui.Sample.PageModels;

public partial class TenPageModel : ObservableObject
{
    private static int _instanceCount;

    public string Message { get; } = "This is page ten - custom tab bar works!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
}

