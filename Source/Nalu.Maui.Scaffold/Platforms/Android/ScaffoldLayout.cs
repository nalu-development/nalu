using Android.Content;
using Android.Runtime;
using Android.Widget;

namespace Nalu;

/// <summary>
/// Platform root of a scaffold-hosted app: a plain FrameLayout — match-parent children
/// (the presenter's fragment container, future chrome layers) are measured and laid out
/// natively, no manual layout plumbing required.
/// </summary>
public sealed class ScaffoldLayout : FrameLayout
{
    /// <summary>Activation constructor.</summary>
    public ScaffoldLayout(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <summary>Initializes a new <see cref="ScaffoldLayout"/>.</summary>
    public ScaffoldLayout(Context context)
        : base(context)
    {
        SetClipChildren(false);
    }
}
