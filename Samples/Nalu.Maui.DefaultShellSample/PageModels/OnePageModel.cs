namespace Nalu.Maui.Sample.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class OnePageModel : ObservableObject
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PushThreeAsync() => Shell.Current.GoToAsync("Three");
}
