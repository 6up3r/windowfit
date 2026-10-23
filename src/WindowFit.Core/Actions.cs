namespace WindowFit.Core;

/// <summary>What a move needs to know about the active window; Frame is its visible frame.</summary>
public sealed record WindowState(Rect Frame, bool Normal, bool Movable, bool Resizable);

public readonly record struct Modifiers(bool Win, bool Ctrl, bool Shift, bool Alt);

/// <summary>
/// A Win+arrow action: Plan returns the visible target rect, or null to leave the window alone.
/// A quarter also names PairKey, the perpendicular arrow held with VirtualKey in either order.
/// </summary>
public sealed record WindowAction(string Name, int VirtualKey, bool Ctrl, Func<Rect, WindowState?, Rect?> Plan, int? PairKey = null);

public static class Actions
{
    private const int VkLeft = 0x25, VkUp = 0x26, VkRight = 0x27, VkDown = 0x28;

    public static IReadOnlyList<WindowAction> All { get; } =
    [
        FitTile("FitHalfLeft", VkLeft, false, Tile.Left, Geometry.GapHalf),
        FitTile("FitHalfRight", VkRight, false, Tile.Right, Geometry.GapHalf),
        FitTile("FitHalfCenter", VkUp, false, Tile.Center, Geometry.GapHalf),
        new("CenterOnly", VkDown, false, CentreOnly),
        FitTile("FillHalfLeft", VkLeft, true, Tile.Left, Geometry.GapNone),
        FitTile("FillHalfRight", VkRight, true, Tile.Right, Geometry.GapNone),
        FitTile("FillScreen", VkUp, true, Tile.Screen, Geometry.GapNone),
        FitTile("FitScreen", VkDown, true, Tile.Screen, Geometry.GapScreen, 2 * Geometry.GapScreen),
        FitTile("FitQuarterTopLeft", VkUp, false, Tile.TopLeft, Geometry.GapHalf, pair: VkLeft),
        FitTile("FitQuarterTopRight", VkUp, false, Tile.TopRight, Geometry.GapHalf, pair: VkRight),
        FitTile("FitQuarterBottomLeft", VkDown, false, Tile.BottomLeft, Geometry.GapHalf, pair: VkLeft),
        FitTile("FitQuarterBottomRight", VkDown, false, Tile.BottomRight, Geometry.GapHalf, pair: VkRight),
        FitTile("FillQuarterTopLeft", VkUp, true, Tile.TopLeft, Geometry.GapNone, pair: VkLeft),
        FitTile("FillQuarterTopRight", VkUp, true, Tile.TopRight, Geometry.GapNone, pair: VkRight),
        FitTile("FillQuarterBottomLeft", VkDown, true, Tile.BottomLeft, Geometry.GapNone, pair: VkLeft),
        FitTile("FillQuarterBottomRight", VkDown, true, Tile.BottomRight, Geometry.GapNone, pair: VkRight),
    ];

    public static WindowAction ByName(string name) => All.Single(a => a.Name == name);

    /// <summary>
    /// The action for an arrow pressed with Win, optionally Ctrl, and neither Shift nor Alt: a quarter
    /// when a <paramref name="held"/> arrow pairs with it, otherwise the arrow's own action.
    /// </summary>
    public static WindowAction? Match(int virtualKey, Modifiers modifiers, IReadOnlyCollection<int>? held = null)
    {
        if (!modifiers.Win || modifiers.Shift || modifiers.Alt)
        {
            return null;
        }
        var candidates = All.Where(a => a.Ctrl == modifiers.Ctrl).ToList();
        return candidates.FirstOrDefault(a => a.PairKey is { } pair && (held ?? []).Any(h =>
                   (a.VirtualKey, pair) == (virtualKey, h) || (a.VirtualKey, pair) == (h, virtualKey)))
               ?? candidates.FirstOrDefault(a => a.PairKey is null && a.VirtualKey == virtualKey);
    }

    private static WindowAction FitTile(string name, int vk, bool ctrl, Tile tile, double gap, double? side = null, int? pair = null) =>
        new(name, vk, ctrl, (area, win) =>
            win is { Normal: true, Movable: true, Resizable: true } ? Geometry.Fit(area, tile, gap, side) : null, pair);

    private static Rect? CentreOnly(Rect area, WindowState? win) =>
        win is { Normal: true, Movable: true } ? Geometry.Centre(area, win.Frame.Width, win.Frame.Height) : null;
}
