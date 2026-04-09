namespace Nalu;

/// <summary>
/// <see cref="PathF"/> extension methods to allow for path construction using relative coordinates
/// </summary>
public static class NaluGraphicsPathExtensions
{
    /// <summary>
    /// Adds a line to the path from the current coordinates to the specified coordinates, where the specified coordinates are relative to the current coordinates.
    /// </summary>
    /// <param name="path">The path to which the line will be added.</param>
    /// <param name="x">The X coordinate of the end point of the line, relative to the current X coordinate.</param>
    /// <param name="y">The Y coordinate of the end point of the line, relative to the current Y coordinate.</param>
    /// <returns>The updated path.</returns>
    public static PathF RelativeLineTo(this PathF path, float x, float y)
    {
        var lastPoint = path.LastPoint;
        x += lastPoint.X;
        y += lastPoint.Y;

        path.LineTo(x, y);

        return path;
    }

    /// <summary>
    /// Adds a quadratic Bézier curve segment using coordinate values.
    /// </summary>
    /// <param name="path">The path to which the line will be added.</param>
    /// <param name="cx">X-coordinate of the control point.</param>
    /// <param name="cy">Y-coordinate of the control point.</param>
    /// <param name="x">X-coordinate of the end point.</param>
    /// <param name="y">Y-coordinate of the end point.</param>
    /// <returns>The current path.</returns>
    public static PathF RelativeQuadTo(this PathF path, float cx, float cy, float x, float y)
    {
        var lastPoint = path.LastPoint;
        var lastPointX = lastPoint.X;
        var lastPointY = lastPoint.Y;
        cx += lastPointX;
        cy += lastPointY;
        x += lastPointX;
        y += lastPointY;

        path.QuadTo(cx, cy, x, y);
        
        return path;
    }

    /// <summary>
    /// Adds a cubic Bézier curve segment using coordinate values.
    /// </summary>
    /// <param name="path">The path to which the line will be added.</param>
    /// <param name="c1X">X-coordinate of the first control point.</param>
    /// <param name="c1Y">Y-coordinate of the first control point.</param>
    /// <param name="c2X">X-coordinate of the second control point.</param>
    /// <param name="c2Y">Y-coordinate of the second control point.</param>
    /// <param name="x">X-coordinate of the end point.</param>
    /// <param name="y">Y-coordinate of the end point.</param>
    /// <returns>The current path.</returns>
    public static PathF RelativeCurveTo(this PathF path, float c1X, float c1Y, float c2X, float c2Y, float x, float y)
    {
        var lastPoint = path.LastPoint;
        var lastPointX = lastPoint.X;
        var lastPointY = lastPoint.Y;
        c1X += lastPointX;
        c1Y += lastPointY;
        c2X += lastPointX;
        c2Y += lastPointY;
        x += lastPointX;
        y += lastPointY;

        path.CurveTo(c1X, c1Y, c2X, c2Y, x, y);
        
        return path;
    }
}
