namespace Nalu.Maui.DailyHelper;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
        => new(_serviceProvider.GetRequiredService<AppScaffold>());
}
