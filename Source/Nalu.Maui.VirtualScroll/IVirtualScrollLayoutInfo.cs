namespace Nalu;

/// <summary>
/// Information about headers and footers in a virtual scroll layout.
/// </summary>
internal interface IVirtualScrollLayoutInfo : IEquatable<IVirtualScrollLayoutInfo>
{
    bool HasGlobalHeader { get; }
    bool HasGlobalFooter { get; }
    bool HasSectionHeader { get; }
    bool HasSectionFooter { get; }
}
