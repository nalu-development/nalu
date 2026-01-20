namespace Nalu;

/// <summary>
/// Wrapper for virtual scroll items used by ItemsRepeater on Windows.
/// </summary>
/// <remarks>
/// ItemsRepeater wants objects, it doesn't work with value types.
/// </remarks>
internal record VirtualScrollItemWrapper(int FlattenedIndex, VirtualScrollFlattenedItem Item);
