using WindowFit.Core;

namespace WindowFit.Tests;

public class FrameCompensationTests
{
    [Fact]
    public void InvisibleBorders_AreAddedBackPerEdge()
    {
        var window = new Rect(100, 200, 814, 607);
        var frame = new Rect(107, 200, 800, 600);

        Assert.Equal(new Rect(34, 41, 1214, 1319), FrameCompensation.WindowRectFor(new Rect(41, 41, 1200, 1312), window, frame));
    }

    [Fact]
    public void BorderlessWindow_IsPlacedAsIs()
    {
        var r = new Rect(0, 0, 640, 480);
        Assert.Equal(new Rect(41, 41, 1200, 1312), FrameCompensation.WindowRectFor(new Rect(41, 41, 1200, 1312), r, r));
    }
}
