namespace Nalu;

#if IOS || ANDROID

/// <summary>
/// Handler for <see cref="Scaffold"/>: a deliberately lean <c>ViewHandler</c> (NOT a
/// <c>PageHandler</c> — the page pipeline carries hidden behaviors the scaffold must own
/// itself, mirroring how Shell ships its own renderer). It owns the platform root view
/// (and, on iOS, the root view controller used for child-page containment), the platform
/// presenter's lifetime (one per connection), and the navigation engine bootstrap.
/// Registered by <see cref="ScaffoldAppBuilderExtensions.UseNaluScaffold"/>.
/// </summary>
public partial class ScaffoldHandler;

#else

/// <summary>
/// Neutral-platform handler for <see cref="Scaffold"/>: registered on every platform, but
/// creating the platform view throws <see cref="PlatformNotSupportedException"/> — the
/// scaffold is realized on iOS and Android only.
/// </summary>
public partial class ScaffoldHandler : Microsoft.Maui.Handlers.ViewHandler<Scaffold, object>
{
    /// <summary>Initializes a new <see cref="ScaffoldHandler"/>.</summary>
    public ScaffoldHandler()
        : base(ViewMapper)
    {
    }

    /// <inheritdoc />
    protected override object CreatePlatformView()
        => throw new PlatformNotSupportedException("The Nalu Scaffold is supported on iOS and Android only.");
}

#endif
