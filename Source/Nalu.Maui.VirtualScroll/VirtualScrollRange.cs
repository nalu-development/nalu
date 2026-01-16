namespace Nalu;

/// <summary>
/// Represents a range of visible items in a VirtualScroll.
/// </summary>
public readonly record struct VirtualScrollRange : IConvertible
{
    /// <summary>
    /// Special section index value indicating the global header.
    /// Using <see cref="int.MinValue"/> ensures it comes first in natural ordering.
    /// </summary>
    public const int GlobalHeaderSectionIndex = int.MinValue;

    /// <summary>
    /// Special section index value indicating the global footer.
    /// Using <see cref="int.MaxValue"/> ensures it comes last in natural ordering.
    /// </summary>
    public const int GlobalFooterSectionIndex = int.MaxValue;

    /// <summary>
    /// Special item index value indicating a section header.
    /// Using <see cref="int.MinValue"/> ensures it comes first within a section in natural ordering.
    /// </summary>
    public const int SectionHeaderItemIndex = int.MinValue;

    /// <summary>
    /// Special item index value indicating a section footer.
    /// Using <see cref="int.MaxValue"/> ensures it comes last within a section in natural ordering.
    /// </summary>
    public const int SectionFooterItemIndex = int.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollRange"/> struct.
    /// </summary>
    /// <param name="startSectionIndex">The section index of the first visible item. Use <see cref="GlobalHeaderSectionIndex"/> for global header, <see cref="GlobalFooterSectionIndex"/> for global footer.</param>
    /// <param name="startItemIndex">The item index within the start section of the first visible item. Use <see cref="SectionHeaderItemIndex"/> for section header, <see cref="SectionFooterItemIndex"/> for section footer.</param>
    /// <param name="endSectionIndex">The section index of the last visible item. Use <see cref="GlobalHeaderSectionIndex"/> for global header, <see cref="GlobalFooterSectionIndex"/> for global footer.</param>
    /// <param name="endItemIndex">The item index within the end section of the last visible item. Use <see cref="SectionHeaderItemIndex"/> for section header, <see cref="SectionFooterItemIndex"/> for section footer.</param>
    public VirtualScrollRange(int startSectionIndex, int startItemIndex, int endSectionIndex, int endItemIndex)
    {
        StartSectionIndex = startSectionIndex;
        StartItemIndex = startItemIndex;
        EndSectionIndex = endSectionIndex;
        EndItemIndex = endItemIndex;
    }

    /// <summary>
    /// Gets the section index of the first visible item.
    /// </summary>
    public int StartSectionIndex { get; }

    /// <summary>
    /// Gets the item index within the start section of the first visible item.
    /// </summary>
    public int StartItemIndex { get; }

    /// <summary>
    /// Gets the section index of the last visible item.
    /// </summary>
    public int EndSectionIndex { get; }

    /// <summary>
    /// Gets the item index within the end section of the last visible item.
    /// </summary>
    public int EndItemIndex { get; }

    /// <summary>
    /// Implicitly converts a <see cref="VirtualScrollRange"/> to an item index on the first section.
    /// </summary>
    public static implicit operator int(VirtualScrollRange range)
    {
        var rangeStartItemIndex = range.StartItemIndex;
        if (rangeStartItemIndex is SectionFooterItemIndex or SectionHeaderItemIndex)
        {
            throw new InvalidOperationException("Cannot convert VirtualScrollRange to an item index when StartItemIndex is a special value (SectionHeaderItemIndex or SectionFooterItemIndex).");
        }

        return rangeStartItemIndex;
    }
    
    /// <summary>
    /// Implicitly converts an item index on the first section to a <see cref="VirtualScrollRange"/>.
    /// </summary>
    public static implicit operator VirtualScrollRange(int itemIndex)
    {
        if (itemIndex is SectionFooterItemIndex or SectionHeaderItemIndex)
        {
            throw new InvalidOperationException("Cannot convert item index to VirtualScrollRange when itemIndex is a special value (SectionHeaderItemIndex or SectionFooterItemIndex).");
        }
        
        return new VirtualScrollRange(0, itemIndex, 0, itemIndex);
    }

    TypeCode IConvertible.GetTypeCode() => TypeCode.Object;

    bool IConvertible.ToBoolean(IFormatProvider? provider) => throw new InvalidOperationException();

    byte IConvertible.ToByte(IFormatProvider? provider) => throw new InvalidOperationException();

    char IConvertible.ToChar(IFormatProvider? provider) => throw new InvalidOperationException();

    DateTime IConvertible.ToDateTime(IFormatProvider? provider) => throw new InvalidOperationException();

    decimal IConvertible.ToDecimal(IFormatProvider? provider) => throw new InvalidOperationException();

    double IConvertible.ToDouble(IFormatProvider? provider) => throw new InvalidOperationException();

    short IConvertible.ToInt16(IFormatProvider? provider) => throw new InvalidOperationException();

    int IConvertible.ToInt32(IFormatProvider? provider) => this;

    long IConvertible.ToInt64(IFormatProvider? provider) => throw new InvalidOperationException();

    sbyte IConvertible.ToSByte(IFormatProvider? provider) => throw new InvalidOperationException();

    float IConvertible.ToSingle(IFormatProvider? provider) => throw new InvalidOperationException();

    string IConvertible.ToString(IFormatProvider? provider) => throw new InvalidOperationException();

    object IConvertible.ToType(Type conversionType, IFormatProvider? provider) => throw new InvalidOperationException();

    ushort IConvertible.ToUInt16(IFormatProvider? provider) => throw new InvalidOperationException();

    uint IConvertible.ToUInt32(IFormatProvider? provider) => throw new InvalidOperationException();

    ulong IConvertible.ToUInt64(IFormatProvider? provider) => throw new InvalidOperationException();
}

