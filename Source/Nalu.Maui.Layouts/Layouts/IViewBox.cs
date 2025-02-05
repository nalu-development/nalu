namespace Nalu;

/// <summary>
/// <see cref="ViewBoxBase"/> is a base class a <see cref="IViewBox"/> that is used to display a single view and supports clipping.
/// </summary>
public interface IViewBox : IContentView
{
    /// <summary>
    /// Specifies whether the <see cref="IViewBox"/> clips its content to its boundaries
    /// </summary>
    public bool ClipsToBounds { get; }
}
