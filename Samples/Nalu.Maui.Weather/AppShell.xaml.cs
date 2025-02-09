namespace Nalu.Maui.Weather;

using PageModels;
using Pages;

public partial class AppShell
{
    public AppShell(INavigationService navigationService) : base(navigationService, typeof(InitializationPage), new StartupIntent())
    {
        InitializeComponent();
    }
}
