namespace Nalu.Maui.Sample;

public partial class App : Application
{
    private readonly INavigationService _navigationService;

    public App(INavigationService navigationService)
    {
        _navigationService = navigationService;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(new AppShell(_navigationService));
}
