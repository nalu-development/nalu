namespace Nalu.Maui.Sample;

public partial class AppShellTabBar
{
    static AppShellTabBar()
    {
#if IOS || ANDROID || MACCATALYST
        NaluTabBar.UseBlurEffect = true;
#endif
    }
    
    public AppShellTabBar()
    {
        InitializeComponent();
    }
}

