namespace Nalu.Maui.Sample;

using Pages;

public partial class AppShell : NaluShell
{
    public AppShell(INavigationService navigationService) : base(navigationService, typeof(OnePage))
    {
        InitializeComponent();
    }
}

