namespace Nalu.Maui.DefaultShellSample.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class FourPageModel : ObservableObject
{
    private static int _instanceCount;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PopToOneAsync() => Shell.Current.GoToAsync("//One");

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task NavigateToTwoAsync() => Shell.Current.GoToAsync("//Two");

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task NavigateToFiveAsync() => Shell.Current.GoToAsync("//Five");
}
