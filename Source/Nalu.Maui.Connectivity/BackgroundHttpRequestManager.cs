namespace Nalu;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using Tenray.ZoneTree;
using Tenray.ZoneTree.Options;

internal sealed class BackgroundHttpRequestManager : IBackgroundHttpRequestManager
{
    private readonly string _rootPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "nalu", "bhrm");

    private readonly IZoneTree<long, BackgroundHttpRequestDescriptor> _storage;
    private readonly ConcurrentDictionary<string, BackgroundHttpRequestHandle> _handles;
    private long _lastRequestId = Stopwatch.GetTimestamp();

    public BackgroundHttpRequestManager(IAppEncryptionProvider appEncryptionProvider)
    {
        var (storage, handles) = CreateContext(appEncryptionProvider);
        _storage = storage;
        _handles = handles;
    }

    public long NewRequestId() => Interlocked.Increment(ref _lastRequestId);

    public BackgroundHttpRequestHandle? GetHandle(string requestName) => _handles.GetValueOrDefault(requestName);

    public void Save(BackgroundHttpRequestHandle handle)
    {
        var descriptor = handle.Descriptor;
        _storage.AtomicUpsert(descriptor.RequestId, descriptor);
    }

    public IEnumerable<BackgroundHttpRequestHandle> GetHandles() => _handles.Values;

    public void Untrack(BackgroundHttpRequestHandle backgroundHttpRequestHandle)
    {
        _storage.TryDelete(backgroundHttpRequestHandle.RequestId, out _);
        _handles.TryRemove(backgroundHttpRequestHandle.RequestName, out _);
    }

    public void Track(BackgroundHttpRequestHandle backgroundHttpRequestHandle)
    {
        var descriptor = backgroundHttpRequestHandle.Descriptor;
        _storage.AtomicUpsert(descriptor.RequestId, descriptor);
        _handles[backgroundHttpRequestHandle.RequestName] = backgroundHttpRequestHandle;
    }

    private (IZoneTree<long, BackgroundHttpRequestDescriptor> Storage, ConcurrentDictionary<string, BackgroundHttpRequestHandle> Handles) CreateContext(IAppEncryptionProvider appEncryptionProvider)
    {
        var encryption = appEncryptionProvider.GetSymmetricAlgorithm();
        var serializer = new BackgroundHttpRequestDescriptorZoneTreeSerializer(encryption);
        var storage = new ZoneTreeFactory<long, BackgroundHttpRequestDescriptor>()
            .SetValueSerializer(serializer)
            .SetDataDirectory(_rootPath)
            .ConfigureWriteAheadLogOptions(o =>
            {
                var supportsWal = Sse42.X64.IsSupported || Sse42.IsSupported || Crc32.Arm64.IsSupported;
                o.CompressionMethod = CompressionMethod.LZ4;
                o.EnableIncrementalBackup = true;
                o.WriteAheadLogMode = supportsWal ? WriteAheadLogMode.Sync : WriteAheadLogMode.None;
            })
            .OpenOrCreate();

        var handles = new ConcurrentDictionary<string, BackgroundHttpRequestHandle>();
        var iterator = storage.CreateIterator();
        while (iterator.Next())
        {
            var (_, descriptor) = iterator.Current;
            var handle = new BackgroundHttpRequestHandle(this, descriptor);
            handles[descriptor.RequestName] = handle;
        }

        return (storage, handles);
    }
}
