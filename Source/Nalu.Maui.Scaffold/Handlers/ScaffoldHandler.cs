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
/// creating the platform element throws <see cref="PlatformNotSupportedException"/> — the
/// scaffold is realized on iOS and Android only.
/// </summary>
/// <remarks>
/// Deliberately a bare <see cref="IElementHandler"/> implementation with NO platform-typed
/// generics: a <c>ViewHandler&lt;Scaffold, object&gt;</c> is legal to compile here but cannot LOAD
/// against a platform runtime (there <c>TPlatformView</c> is constrained to the platform view
/// type), and this neutral assembly is exactly what a Mac Catalyst or Windows app binds through
/// TFM fallback — merely registering the handler in <c>UseNaluScaffold</c> was a startup
/// <see cref="TypeLoadException"/> crash until this class stopped touching those generics
/// (MAUI's non-generic <c>ElementHandler</c> base is not derivable outside the framework: its
/// abstract members are <c>private protected</c>).
/// </remarks>
public partial class ScaffoldHandler : IElementHandler
{
    /// <inheritdoc />
    public IMauiContext? MauiContext { get; private set; }

    /// <inheritdoc />
    public object? PlatformView => null;

    /// <inheritdoc />
    public IElement? VirtualView { get; private set; }

    /// <inheritdoc />
    public void SetMauiContext(IMauiContext mauiContext) => MauiContext = mauiContext;

    /// <inheritdoc />
    public void SetVirtualView(IElement view)
        => throw new PlatformNotSupportedException("The Nalu Scaffold is supported on iOS and Android only.");

    /// <inheritdoc />
    public void UpdateValue(string property)
    {
    }

    /// <inheritdoc />
    public void Invoke(string command, object? args = null)
    {
    }

    /// <inheritdoc />
    public void DisconnectHandler() => VirtualView = null;
}

#endif
