namespace Nalu;

#pragma warning disable SA1602,CS1591
[Flags]
public enum AnchorType
{
    Undefined = 0x0000,

    EndToStartOf = 0x0001,
    StartToEndOf = 0x0002,
    SourceTop = 0x0010,
    SourceBottom = 0x0020,
    SourceLeft = 0x0040,
    SourceRight = 0x0080,
    TargetTop = 0x0100,
    TargetBottom = 0x0200,
    TargetLeft = 0x0400,
    TargetRight = 0x0800,

    TopToTopOf = SourceTop | TargetTop,
    BottomToBottomOf = SourceBottom | TargetBottom,
    LeftToLeftOf = SourceLeft | TargetLeft,
    RightToRightOf = SourceRight | TargetRight,
    TopToBottomOf = SourceTop | TargetBottom | StartToEndOf,
    BottomToTopOf = SourceBottom | TargetTop | EndToStartOf,
    LeftToRightOf = SourceLeft | TargetRight | StartToEndOf,
    RightToLeftOf = SourceRight | TargetLeft | EndToStartOf,
}
