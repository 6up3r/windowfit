using WindowFit.Core;

namespace WindowFit.Tests;

public class GuardTests
{
    private static readonly Rect Area = new(0, 0, 2560, 1394);
    private static readonly WindowState Start = new(new Rect(10, 20, 700, 500), Normal: true, Movable: true, Resizable: true);

    [Fact]
    public void FitHalfCenter_IgnoresNonResizableWindow() =>
        Assert.Null(Actions.ByName("FitHalfCenter").Plan(Area, Start with { Resizable = false }));

    [Fact]
    public void FitHalfCenter_IgnoresNonNormalWindow() =>
        Assert.Null(Actions.ByName("FitHalfCenter").Plan(Area, Start with { Normal = false }));

    [Fact]
    public void CenterOnly_IgnoresNonMovableWindow() =>
        Assert.Null(Actions.ByName("CenterOnly").Plan(Area, Start with { Movable = false }));

    [Fact]
    public void CenterOnly_ActsOnNonResizableWindow() =>
        Assert.Equal(new Rect(930, 447, 700, 500), Actions.ByName("CenterOnly").Plan(Area, Start with { Resizable = false }));

    [Fact]
    public void Fit_RatioEatingTheHeight_LeavesWindowAlone() =>
        Assert.Null(Geometry.Fit(Area, Tile.Screen, 0.6));

    [Fact]
    public void Fit_SideRatioEatingTheWidth_LeavesWindowAlone() =>
        Assert.Null(Geometry.Fit(Area, Tile.Left, 0.03, 0.6));

    [Fact]
    public void NoActiveWindow_IsIgnoredWithoutException()
    {
        Assert.Null(Actions.ByName("FitHalfCenter").Plan(Area, null));
        Assert.Null(Actions.ByName("CenterOnly").Plan(Area, null));
    }

    [Fact]
    public void FitQuarter_IgnoresNonResizableWindow() =>
        Assert.Null(Actions.ByName("FitQuarterTopLeft").Plan(Area, Start with { Resizable = false }));

    [Fact]
    public void Contract_ExactlyTheSixteenActionsAreRegistered() =>
        Assert.Equal(
            new[]
            {
                "FitHalfLeft", "FitHalfRight", "FitHalfCenter", "CenterOnly", "FillHalfLeft", "FillHalfRight", "FillScreen", "FitScreen",
                "FitQuarterTopLeft", "FitQuarterTopRight", "FitQuarterBottomLeft", "FitQuarterBottomRight",
                "FillQuarterTopLeft", "FillQuarterTopRight", "FillQuarterBottomLeft", "FillQuarterBottomRight",
            },
            Actions.All.Select(a => a.Name));
}
