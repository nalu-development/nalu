namespace Nalu.Maui.Sample.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public class AnimalModel
{
    public string Name { get; set; }
}

public partial class OnePageModel(INavigationService navigationService) : ObservableObject
{
    private static int _instanceCount;

    public AnimalModel Animal { get; } = new AnimalModel { Name = "Dog" };

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand(AllowConcurrentExecutions = false)]
    private Task PushThreeAsync() => navigationService.GoToAsync(Navigation.Relative().Push<ThreePageModel>());
}
