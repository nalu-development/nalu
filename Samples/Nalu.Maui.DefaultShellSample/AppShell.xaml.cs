namespace Nalu.Maui.DefaultShellSample;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        this.ConfigureForPageDisposal(disposeShellContents: true);
    }
}
