namespace Nalu;

internal interface IShellItemProxy
{
    string SegmentName { get; }
    IShellSectionProxy CurrentSection { get; }
    IReadOnlyList<IShellSectionProxy> Sections { get; }
    IShellProxy Parent { get; }
}
