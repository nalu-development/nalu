namespace Nalu;

using Android.Content;

/// <summary>
/// Provides easy access to the Android application information.
/// </summary>
public static class AndroidApp
{
    private static readonly Dictionary<Type, bool> _foregroundServicesStarted = [];
    private static int _notificationIdCounter = 9999;
    private static Context? _applicationContext;

    /// <summary>
    /// Gets the application context.
    /// </summary>
    public static Context GetApplicationContext()
        => _applicationContext ??= Android.App.Application.Context;

    /// <summary>
    /// Creates a new notification identifier.
    /// </summary>
    public static int NewNotificationId() => Interlocked.Increment(ref _notificationIdCounter);

    /// <summary>
    /// Gets the resource identifier for a drawable by name.
    /// </summary>
    /// <param name="name">The resource name.</param>
    public static int GetResourceDrawableIdByName(string name)
    {
        var applicationContext = GetApplicationContext();
        return applicationContext
            .Resources?
            .GetIdentifier(
                name,
                "drawable",
                applicationContext.PackageName) ?? 0;
    }

    /// <summary>
    /// Starts a foreground service.
    /// </summary>
    /// <param name="serviceType">The foreground service type.</param>
    public static void StartForegroundService(Type serviceType)
    {
        if (!serviceType.IsAssignableTo(typeof(AndroidForegroundServiceBase)))
        {
            throw CreateUnsupportedServiceTypeException(serviceType);
        }

        lock (_foregroundServicesStarted)
        {
            if (_foregroundServicesStarted.TryGetValue(serviceType, out var started) && started)
            {
                return;
            }

            var applicationContext = GetApplicationContext();
            var intent = new Intent(applicationContext, serviceType);
            intent.SetAction(AndroidForegroundService.StartAction);

            if (OperatingSystem.IsAndroidVersionAtLeast(31))
            {
                applicationContext.StartForegroundService(intent);
            }
            else
            {
                applicationContext.StartService(intent);
            }

            _foregroundServicesStarted[serviceType] = true;
        }
    }

    /// <summary>
    /// Stops a <see cref="AndroidForegroundServiceBase{TBackgroundService}"/>.
    /// </summary>
    /// <param name="serviceType">The foreground service type.</param>
    public static void StopForegroundService(Type serviceType)
    {
        if (!serviceType.IsAssignableTo(typeof(AndroidForegroundServiceBase)))
        {
            throw CreateUnsupportedServiceTypeException(serviceType);
        }

        lock (_foregroundServicesStarted)
        {
            if (!_foregroundServicesStarted.TryGetValue(serviceType, out var started) || !started)
            {
                return;
            }

            var applicationContext = GetApplicationContext();
            var intent = new Intent(applicationContext, serviceType);
            intent.SetAction(AndroidForegroundService.StopAction);
            applicationContext.StartService(intent);

            _foregroundServicesStarted[serviceType] = false;
        }
    }

    private static ArgumentException CreateUnsupportedServiceTypeException(Type serviceType) => new($"The service type {serviceType.FullName} must be a subclass of AndroidForegroundServiceBase.", nameof(serviceType));
}
