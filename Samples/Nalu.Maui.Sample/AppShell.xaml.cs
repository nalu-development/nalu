namespace Nalu.Maui.Sample;

using PageModels;

public partial class AppShell : NaluShell
{
    public AppShell()
    {
        InitializeComponent();
        ConfigureNavigation<OnePageModel>();
    }
}

