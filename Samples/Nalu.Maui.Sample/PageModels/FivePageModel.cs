using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class MyItem : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isExpanded;

    [RelayCommand]
    private void Toggle() => IsExpanded = !IsExpanded;
}

public partial class FivePageModel(INavigationService navigationService) : ObservableObject
{
    public List<MyItem> Items { get; } = Enumerable
                                         .Range(1, 100)
                                         .Select(i => new MyItem
                                                      {
                                                          Name = $"Item {i}",
                                                          Description =
                                                              $"Lorem ipsum dolor sit amet {i}, consectetur adipiscing elit {i}. Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua {i}. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat {i}. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur {i}. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum {i}."
                                                      }
                                         )
                                         .ToList();

    private static int _instanceCount;

    [ObservableProperty]
    private bool _condition;

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    [RelayCommand]
    private void Toggle() => Condition = !Condition;

    [RelayCommand]
    private Task GoToThreeAsync() => navigationService.GoToAsync(Navigation.Absolute().Root<OnePageModel>().Add<ThreePageModel>());
}
