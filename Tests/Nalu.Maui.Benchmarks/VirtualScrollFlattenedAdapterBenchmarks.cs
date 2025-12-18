using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Nalu.Maui.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90, warmupCount: 5, iterationCount: 15, invocationCount: 1000)]
public class VirtualScrollFlattenedAdapterBenchmarks
{
    private StubAdapter _adapter = null!;
    private StubLayoutInfo _layoutInfo = null!;
    private VirtualScrollFlattenedAdapter _flattenedAdapter = null!;

    [Params(10, 50, 100)]
    public int SectionCount { get; set; }

    [Params(10, 100, 1000)]
    public int ItemsPerSection { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _adapter = new StubAdapter(SectionCount, ItemsPerSection);
        _layoutInfo = new StubLayoutInfo(false, false, false, false);

        _flattenedAdapter = new VirtualScrollFlattenedAdapter(_adapter, _layoutInfo);
    }

    [Benchmark]
    public void GetItem()
    {
        var index = (SectionCount * ItemsPerSection) / 2;
        for (var i = 0; i < 10000; i++)
        {
            _ = _flattenedAdapter.GetItem(index);
        }
    }

    [Benchmark]
    public int GetItemCount()
    {
        var result = 0;
        for (var i = 0; i < 10000; i++)
        {
            result = _flattenedAdapter.GetItemCount();
        }
        return result;
    }

    // Note: Change operations require actual adapter callbacks which is complex to benchmark accurately
    // These benchmarks focus on the core GetItem and GetItemCount operations
}

// Stub implementations to avoid mock overhead
internal sealed class StubAdapter : IVirtualScrollAdapter
{
    private readonly int _sectionCount;
    private readonly int _itemsPerSection;

    public StubAdapter(int sectionCount, int itemsPerSection)
    {
        _sectionCount = sectionCount;
        _itemsPerSection = itemsPerSection;
    }

    public int GetSectionCount() => _sectionCount;

    public int GetItemCount(int sectionIndex) => _itemsPerSection;

    public object? GetSection(int sectionIndex) => null;

    public object? GetItem(int sectionIndex, int itemIndex) => ((long)sectionIndex << 32) | (uint)itemIndex;

    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback) => new StubDisposable();
}

internal sealed class StubLayoutInfo : IVirtualScrollLayoutInfo
{
    public bool HasGlobalHeader { get; }
    public bool HasGlobalFooter { get; }
    public bool HasSectionHeader { get; }
    public bool HasSectionFooter { get; }

    public StubLayoutInfo(bool hasGlobalHeader, bool hasGlobalFooter, bool hasSectionHeader, bool hasSectionFooter)
    {
        HasGlobalHeader = hasGlobalHeader;
        HasGlobalFooter = hasGlobalFooter;
        HasSectionHeader = hasSectionHeader;
        HasSectionFooter = hasSectionFooter;
    }

    public bool Equals(IVirtualScrollLayoutInfo? other)
    {
        if (other == null)
        {
            return false;
        }
        return HasGlobalHeader == other.HasGlobalHeader &&
               HasGlobalFooter == other.HasGlobalFooter &&
               HasSectionHeader == other.HasSectionHeader &&
               HasSectionFooter == other.HasSectionFooter;
    }
}

internal sealed class StubDisposable : IDisposable
{
    public void Dispose() { }
}

