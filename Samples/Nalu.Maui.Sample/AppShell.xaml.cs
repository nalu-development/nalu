using Nalu.Maui.Sample.Pages;

namespace Nalu.Maui.Sample;

public partial class AppShell : NaluShell
{
    public AppShell(INavigationService navigationService)
        : base(navigationService, typeof(OnePage))
    {
        InitializeComponent();
    }
}
