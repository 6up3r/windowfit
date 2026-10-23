namespace WindowFit.Core;

/// <summary>A screen rectangle in physical pixels, origin top-left.</summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public static Rect FromEdges(int left, int top, int right, int bottom) => new(left, top, right - left, bottom - top);
}
