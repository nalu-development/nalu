namespace Nalu;

internal interface IShellContentProxy
{
    string SegmentName { get; }
    bool HasGuard { get; }
    IShellSectionProxy Parent { get; }
    Page? Page { get; }
    Page GetOrCreateContent();
    void DestroyContent();
}
