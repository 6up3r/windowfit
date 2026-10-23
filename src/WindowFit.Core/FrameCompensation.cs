namespace WindowFit.Core;

/// <summary>
/// SetWindowPos takes the window rect, which includes invisible resize borders that differ per
/// edge and per window state; the visible frame is DWMWA_EXTENDED_FRAME_BOUNDS.
/// </summary>
public static class FrameCompensation
{
    public static Rect WindowRectFor(Rect visibleTarget, Rect windowRect, Rect frameRect) =>
        Rect.FromEdges(
            visibleTarget.X - (frameRect.X - windowRect.X),
            visibleTarget.Y - (frameRect.Y - windowRect.Y),
            visibleTarget.Right + (windowRect.Right - frameRect.Right),
            visibleTarget.Bottom + (windowRect.Bottom - frameRect.Bottom));
}
