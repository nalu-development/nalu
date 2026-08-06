namespace Nalu;

/// <summary>
/// Motion of one page during a transition, declared as target-relative deltas:
/// translation as a fraction of the page size, plus scale and opacity.
/// The entering page animates FROM this state to natural; the behind page TO this state.
/// </summary>
/// <param name="FractionX">Horizontal translation as a fraction of the page width.</param>
/// <param name="FractionY">Vertical translation as a fraction of the page height.</param>
/// <param name="Scale">Uniform scale factor (1 = natural size).</param>
/// <param name="Opacity">Opacity (1 = fully opaque).</param>
public sealed record ScaffoldTransitionMotion(double FractionX = 0, double FractionY = 0, double Scale = 1, double Opacity = 1)
{
    /// <summary>Whether this motion leaves the page exactly in its natural state.</summary>
    public bool IsIdentity => FractionX == 0 && FractionY == 0 && Scale == 1 && Opacity == 1;
}

/// <summary>
/// Cross-platform, declarative push/pop page transition (§8.2): <see cref="Enter"/> describes
/// the incoming page's start state, <see cref="Behind"/> the covered page's end state.
/// Declarative on purpose: both engines interpret it with native animators (iOS animation
/// blocks, Android property animators), keeping every transition seekable and reversible —
/// the pop (and the iOS interactive edge swipe) plays the SAME spec in reverse.
/// Resolution order: page-attached <see cref="Scaffold.PageTransitionProperty"/> →
/// scaffold-level value → <see cref="Default"/>. The spec is resolved from the page that was
/// PUSHED: it enters with it and leaves with it reversed.
/// </summary>
/// <param name="Enter">The incoming page's start state (animated to natural).</param>
/// <param name="Behind">The covered page's end state (animated from natural).</param>
/// <param name="DurationSeconds">Transition duration in seconds.</param>
public sealed record ScaffoldPageTransition(ScaffoldTransitionMotion Enter, ScaffoldTransitionMotion Behind, double DurationSeconds = 0.25)
{
    /// <summary>The stock navigation slide: in from the trailing edge, behind page static.</summary>
    public static ScaffoldPageTransition Default { get; } = new(
        new ScaffoldTransitionMotion(FractionX: 1),
        new ScaffoldTransitionMotion());

    /// <summary>iOS-navigation style: slide in from the trailing edge, behind page parallaxes away.</summary>
    public static ScaffoldPageTransition SlideFromRight { get; } = new(
        new ScaffoldTransitionMotion(FractionX: 1),
        new ScaffoldTransitionMotion(FractionX: -0.3, Opacity: 0.9));

    /// <summary>Subtle slide-up + fade in; the behind page dims and recedes.</summary>
    public static ScaffoldPageTransition SlideUpFade { get; } = new(
        new ScaffoldTransitionMotion(FractionY: 0.03, Opacity: 0),
        new ScaffoldTransitionMotion(Scale: 0.97, Opacity: 0.85),
        0.38);

    /// <summary>Material-ish zoom: scale up + fade in; the behind page grows and dims.</summary>
    public static ScaffoldPageTransition ZoomFade { get; } = new(
        new ScaffoldTransitionMotion(Scale: 0.85, Opacity: 0),
        new ScaffoldTransitionMotion(Scale: 1.05, Opacity: 0.6),
        0.3);

    /// <summary>
    /// Modal presentation: slide up from the bottom edge; the behind page recedes slightly.
    /// The default for modal pages (<see cref="Scaffold.PageModeProperty"/>).
    /// </summary>
    public static ScaffoldPageTransition SlideFromBottom { get; } = new(
        new ScaffoldTransitionMotion(FractionY: 1),
        new ScaffoldTransitionMotion(Scale: 0.97, Opacity: 0.9),
        0.3);

    /// <summary>No animation: pages swap instantly.</summary>
    public static ScaffoldPageTransition None { get; } = new(
        new ScaffoldTransitionMotion(),
        new ScaffoldTransitionMotion(),
        0);

    /// <summary>Whether this spec produces any visible animation.</summary>
    public bool IsAnimated => DurationSeconds > 0 && !(Enter.IsIdentity && Behind.IsIdentity);
}
