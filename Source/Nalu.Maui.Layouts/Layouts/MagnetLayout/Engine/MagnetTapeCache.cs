using System.Text;

namespace Nalu.MagnetLayout.Engine;

/// <summary>
/// Small LRU cache of compiled tapes keyed by the structural fingerprint of a node set.
/// </summary>
internal static class MagnetTapeCache
{
    private const int Capacity = 32;
    private static readonly Lock _lock = new();
    private static readonly Dictionary<string, LinkedListNode<(string Key, MagnetTape Tape)>> _entries = new(StringComparer.Ordinal);
    private static readonly LinkedList<(string Key, MagnetTape Tape)> _lru = [];

    [ThreadStatic]
    private static StringBuilder? _builder;

    public static int Count
    {
        get
        {
            lock (_lock)
            {
                return _entries.Count;
            }
        }
    }

    public static bool TryGet(string key, out MagnetTape tape)
    {
        lock (_lock)
        {
            if (_entries.TryGetValue(key, out var node))
            {
                _lru.Remove(node);
                _lru.AddFirst(node);
                tape = node.Value.Tape;

                return true;
            }
        }

        tape = null!;

        return false;
    }

    public static void Add(string key, MagnetTape tape)
    {
        lock (_lock)
        {
            if (_entries.ContainsKey(key))
            {
                return;
            }

            if (_entries.Count >= Capacity)
            {
                var last = _lru.Last!;
                _lru.RemoveLast();
                _entries.Remove(last.Value.Key);
            }

            var node = _lru.AddFirst((key, tape));
            _entries[key] = node;
        }
    }

    public static void Clear()
    {
        lock (_lock)
        {
            _entries.Clear();
            _lru.Clear();
        }
    }

    /// <summary>
    /// Builds the structural fingerprint: everything the compiler reads except patchable values.
    /// </summary>
    public static string CreateKey(IReadOnlyList<MagnetNode> nodes)
    {
        var sb = _builder ??= new StringBuilder(512);
        sb.Clear();

        for (var i = 0; i < nodes.Count; i++)
        {
            var node = nodes[i];
            sb.Append(node.MagnetId).Append('|');

            switch (node)
            {
                case MagnetView view:
                    sb.Append('V');
                    AppendAnchor(sb, view.LeftTo);
                    AppendAnchor(sb, view.RightTo);
                    AppendAnchor(sb, view.TopTo);
                    AppendAnchor(sb, view.BottomTo);
                    AppendSize(sb, view.WidthSizing);
                    AppendSize(sb, view.HeightSizing);

                    break;

                case MagnetBarrier barrier:
                    sb.Append('B').Append((int) barrier.Direction);
                    AppendList(sb, barrier.Nodes);

                    break;

                case MagnetGuideline guideline:
                    sb.Append('G').Append((int) guideline.Orientation);

                    break;

                case MagnetChain chain:
                    sb.Append('C').Append((int) chain.Orientation).Append((int) chain.Style);
                    AppendList(sb, chain.Nodes);

                    break;

                default:
                    sb.Append('?').Append(node.GetType().FullName);

                    break;
            }

            sb.Append('\n');
        }

        return sb.ToString();
    }

    private static void AppendAnchor(StringBuilder sb, MagnetAnchor? anchor)
    {
        if (anchor is { } a)
        {
            sb.Append(a.Target).Append('.').Append((int) a.Pole);
        }

        sb.Append(';');
    }

    private static void AppendSize(StringBuilder sb, MagnetSizing size)
        => sb.Append((int) size.Unit).Append(size.HasBounds ? 'b' : 'u').Append(';');

    private static void AppendList(StringBuilder sb, IList<string> items)
    {
        foreach (var item in items)
        {
            sb.Append(item).Append(',');
        }

        sb.Append(';');
    }
}
