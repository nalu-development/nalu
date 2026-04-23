namespace Nalu.SharpState;

/// <summary>
/// Lightweight value carrier for trigger arguments. It keeps the hot path allocation-free for the common
/// case where no external handlers need the boxed <see cref="object"/> array representation.
/// </summary>
public readonly struct TriggerArgs
{
    private readonly object? _arg0;
    private readonly object? _arg1;
    private readonly object? _arg2;
    private readonly object? _arg3;

    private TriggerArgs(int count, object? arg0, object? arg1, object? arg2, object? arg3)
    {
        Count = count;
        _arg0 = arg0;
        _arg1 = arg1;
        _arg2 = arg2;
        _arg3 = arg3;
    }

    /// <summary>
    /// An empty argument list.
    /// </summary>
    public static TriggerArgs Empty => default;

    /// <summary>
    /// Number of captured trigger arguments.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// Gets an argument by position.
    /// </summary>
    /// <param name="index">Zero-based argument index.</param>
    public object? this[int index] => index switch
    {
        0 when Count > 0 => _arg0,
        1 when Count > 1 => _arg1,
        2 when Count > 2 => _arg2,
        3 when Count > 3 => _arg3,
        _ => throw new IndexOutOfRangeException($"Trigger argument index {index} is out of range for {Count} argument(s)."),
    };

    /// <summary>
    /// Materializes the arguments into a boxed array for public callbacks.
    /// </summary>
    public object?[] ToArray() => Count switch
    {
        0 => [],
        1 => [_arg0],
        2 => [_arg0, _arg1],
        3 => [_arg0, _arg1, _arg2],
        4 => [_arg0, _arg1, _arg2, _arg3],
        _ => throw new InvalidOperationException($"Unsupported trigger argument count '{Count}'."),
    };

    /// <summary>
    /// Creates a one-argument payload.
    /// </summary>
    public static TriggerArgs From(object? arg0)
        => new(1, arg0, null, null, null);

    /// <summary>
    /// Creates a two-argument payload.
    /// </summary>
    public static TriggerArgs From(object? arg0, object? arg1)
        => new(2, arg0, arg1, null, null);

    /// <summary>
    /// Creates a three-argument payload.
    /// </summary>
    public static TriggerArgs From(object? arg0, object? arg1, object? arg2)
        => new(3, arg0, arg1, arg2, null);

    /// <summary>
    /// Creates a four-argument payload.
    /// </summary>
    public static TriggerArgs From(object? arg0, object? arg1, object? arg2, object? arg3)
        => new(4, arg0, arg1, arg2, arg3);
}
