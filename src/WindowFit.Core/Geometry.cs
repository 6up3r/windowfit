namespace WindowFit.Core;

public enum Tile { Left, Right, Center, Screen, TopLeft, TopRight, BottomLeft, BottomRight }

/// <summary>
/// Tiles of the work area with a gap around them. The gap is a fraction of the shorter side of
/// the work area, so it is the same on all four sides and for every tile; FitScreen alone passes
/// a wider ratio for left and right.
/// </summary>
public static class Geometry
{
    public const double GapNone = 0;
    public const double GapHalf = 0.03;
    public const double GapScreen = 0.09;

    /// <summary>Floors to two decimals: combined ratios such as 0.16499999999999998 stay at the constants' precision.</summary>
    public static double Ratio(double value) => Math.Floor(value * 100) / 100;

    public static Rect TileOf(Rect area, Tile tile)
    {
        // Half rounds up (JavaScript Math.round), not to even: an odd width gives the left half the extra pixel.
        var mid = (int)Math.Floor(area.Width / 2.0 + 0.5);
        var midY = (int)Math.Floor(area.Height / 2.0 + 0.5);
        return tile switch
        {
            Tile.Left => new Rect(area.X, area.Y, mid, area.Height),
            Tile.Right => new Rect(area.X + mid, area.Y, area.Width - mid, area.Height),
            // Half-width box straddling the two halves, as wide as a fitted half.
            Tile.Center => new Rect(area.X + (int)Math.Floor((area.Width - mid) / 2.0), area.Y, mid, area.Height),
            Tile.TopLeft => new Rect(area.X, area.Y, mid, midY),
            Tile.TopRight => new Rect(area.X + mid, area.Y, area.Width - mid, midY),
            Tile.BottomLeft => new Rect(area.X, area.Y + midY, mid, area.Height - midY),
            Tile.BottomRight => new Rect(area.X + mid, area.Y + midY, area.Width - mid, area.Height - midY),
            _ => area,
        };
    }

    /// <summary>The visible rect for <paramref name="tile"/>, or null when the gaps leave no room.</summary>
    public static Rect? Fit(Rect area, Tile tile, double gapRatio, double? sideRatio = null)
    {
        var reference = Math.Min(area.Width, area.Height);
        var gapY = (int)Math.Floor(reference * Ratio(gapRatio));
        var gapX = (int)Math.Floor(reference * Ratio(sideRatio ?? gapRatio));
        var box = TileOf(area, tile);
        var width = box.Width - 2 * gapX;
        var height = box.Height - 2 * gapY;
        if (width <= 0 || height <= 0)
        {
            return null;
        }
        return new Rect(box.X + gapX, box.Y + gapY, width, height);
    }

    /// <summary>Floors towards negative infinity, so a window larger than the area shifts consistently.</summary>
    public static Rect Centre(Rect area, int width, int height) =>
        new((int)Math.Floor(area.X + (area.Width - width) / 2.0), (int)Math.Floor(area.Y + (area.Height - height) / 2.0), width, height);
}
