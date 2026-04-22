using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Nalu.SharpState.Generators.Model;

/// <summary>
/// Value-equatable snapshot of a <see cref="Location"/> safe to carry through the incremental pipeline.
/// </summary>
internal readonly record struct LocationInfo(string FilePath, TextSpan TextSpan, LinePositionSpan LineSpan)
{
    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public static LocationInfo? FromLocation(Location? location)
    {
        if (location is null || location.SourceTree is null)
        {
            return null;
        }

        return new LocationInfo(
            location.SourceTree.FilePath,
            location.SourceSpan,
            location.GetLineSpan().Span);
    }
}
