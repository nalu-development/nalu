namespace Nalu.Maui.DefaultShellSample;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        MainPage = new AppShell();
    }
}
