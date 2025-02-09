using Nalu.Maui.Weather.PageModels;
using Nalu.Maui.Weather.Pages;

namespace Nalu.Maui.Weather;

public partial class AppShell
{
    public AppShell(INavigationService navigationService)
        : base(navigationService, typeof(InitializationPage), new StartupIntent())
    {
        InitializeComponent();
    }
}
