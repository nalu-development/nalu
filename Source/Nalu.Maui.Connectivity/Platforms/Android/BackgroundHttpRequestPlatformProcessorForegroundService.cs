namespace Nalu;

using Android.App;
using Android.Content.PM;

[Service(
    Enabled = true,
    Exported = true,
    ForegroundServiceType = ForegroundService.TypeDataSync)]
public class BackgroundHttpRequestPlatformProcessorForegroundService : AndroidForegroundServiceBase<BackgroundHttpRequestPlatformProcessor>
{
    protected override ForegroundService ForegroundService => ForegroundService.TypeDataSync;
}
