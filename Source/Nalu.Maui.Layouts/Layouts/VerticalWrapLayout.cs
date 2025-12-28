using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// A layout that arranges its children vertically from top to bottom, wrapping to the next column when necessary.
/// </summary>
public class VerticalWrapLayout : WrapLayout
{
    /// <inheritdoc />
    protected override ILayoutManager CreateLayoutManager() => new VerticalWrapLayoutManager(this);
}

