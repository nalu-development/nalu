using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// A layout that arranges its children horizontally from left to right, wrapping to the next row when necessary.
/// </summary>
public class HorizontalWrapLayout : WrapLayout
{
    /// <inheritdoc />
    protected override ILayoutManager CreateLayoutManager() => new HorizontalWrapLayoutManager(this);
}

