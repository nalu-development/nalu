namespace Nalu;

/// <summary>
/// Information about headers and footers in a virtual scroll layout.
/// </summary>
public interface IVirtualScrollLayoutInfo : IEquatable<IVirtualScrollLayoutInfo>
{
    /// <summary>
    /// Gets a value indicating whether the layout has a global header.
    /// </summary>
    bool HasGlobalHeader { get; }
    /// <summary>
    /// Gets a value indicating whether the layout has a global footer.
    /// </summary>
    bool HasGlobalFooter { get; }
    /// <summary>
    /// Gets a value indicating whether the layout has section headers.
    /// </summary>
    bool HasSectionHeader { get; }
    /// <summary>
    /// Gets a value indicating whether the layout has section footers.
    /// </summary>
    bool HasSectionFooter { get; }
}
