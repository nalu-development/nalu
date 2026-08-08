using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nalu.Maui.Navigation.SourceGenerators;

/// <summary>
/// A serializable, equatable stand-in for <see cref="Microsoft.CodeAnalysis.Location"/>:
/// pipeline records must never hold symbols or locations (they root compilations and defeat
/// incremental caching).
/// </summary>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo From(SyntaxNode node)
    {
        var location = node.GetLocation();

        return new LocationInfo(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);
    }
}
