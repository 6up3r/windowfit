using WindowFit.Core;

namespace WindowFit.Tests;

public class GeometryTests
{
    private static readonly WindowState Start = new(new Rect(10, 20, 700, 500), Normal: true, Movable: true, Resizable: true);

    public static TheoryData<Rect> Screens => new()
    {
        new Rect(0, 0, 2560, 1394),
        new Rect(2560, 0, 1920, 1154),
        new Rect(0, 0, 1921, 1153),
    };

    private static int Reference(Rect area) => Math.Min(area.Width, area.Height);

    private static int Gap(Rect area) => (int)Math.Floor(Reference(area) * 0.03);

    private static Rect Run(string action, Rect area) =>
        Actions.ByName(action).Plan(area, Start) ?? throw new Xunit.Sdk.XunitException($"{action} left the window alone");

    private static (Rect TopLeft, Rect TopRight, Rect BottomLeft, Rect BottomRight) Quarters(string prefix, Rect area) =>
        (Run($"{prefix}QuarterTopLeft", area), Run($"{prefix}QuarterTopRight", area),
         Run($"{prefix}QuarterBottomLeft", area), Run($"{prefix}QuarterBottomRight", area));

    [Theory, MemberData(nameof(Screens))]
    public void FitHalfLeft_OuterGapIsGap(Rect area) =>
        Assert.Equal(Gap(area), Run("FitHalfLeft", area).X - area.X);

    [Theory, MemberData(nameof(Screens))]
    public void FitHalfRight_OuterGapIsGap(Rect area) =>
        Assert.Equal(Gap(area), area.Right - Run("FitHalfRight", area).Right);

    [Theory, MemberData(nameof(Screens))]
    public void FitHalfLeft_TopAndBottomAreGap(Rect area)
    {
        var r = Run("FitHalfLeft", area);
        Assert.Equal((Gap(area), Gap(area)), (r.Y - area.Y, area.Bottom - r.Bottom));
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitHalfRight_TopAndBottomAreGap(Rect area)
    {
        var r = Run("FitHalfRight", area);
        Assert.Equal((Gap(area), Gap(area)), (r.Y - area.Y, area.Bottom - r.Bottom));
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitHalves_DoNotOverlap(Rect area) =>
        Assert.True(Run("FitHalfLeft", area).Right <= Run("FitHalfRight", area).X);

    [Theory, MemberData(nameof(Screens))]
    public void FitHalves_CentreSeamIsTwiceGap(Rect area) =>
        Assert.Equal(2 * Gap(area), Run("FitHalfRight", area).X - Run("FitHalfLeft", area).Right);

    [Theory, MemberData(nameof(Screens))]
    public void FitHalves_WidthsDifferByAtMostOne(Rect area) =>
        Assert.InRange(Math.Abs(Run("FitHalfLeft", area).Width - Run("FitHalfRight", area).Width), 0, 1);

    [Theory, MemberData(nameof(Screens))]
    public void FitHalves_SpanAreaInsetByGap(Rect area)
    {
        Assert.Equal(area.X + Gap(area), Run("FitHalfLeft", area).X);
        Assert.Equal(area.Right - Gap(area), Run("FitHalfRight", area).Right);
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitHalfCenter_WidthWithinOneOfBothHalves(Rect area)
    {
        var c = Run("FitHalfCenter", area).Width;
        Assert.InRange(Math.Abs(c - Run("FitHalfLeft", area).Width), 0, 1);
        Assert.InRange(Math.Abs(c - Run("FitHalfRight", area).Width), 0, 1);
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitHalfCenter_SideGapsDifferByAtMostOne(Rect area)
    {
        var r = Run("FitHalfCenter", area);
        Assert.InRange(Math.Abs((r.X - area.X) - (area.Right - r.Right)), 0, 1);
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitHalfCenter_TopAndBottomAreGap(Rect area)
    {
        var r = Run("FitHalfCenter", area);
        Assert.Equal((Gap(area), Gap(area)), (r.Y - area.Y, area.Bottom - r.Bottom));
    }

    [Theory, MemberData(nameof(Screens))]
    public void FillHalves_FlushWithAreaEdges(Rect area)
    {
        Assert.Equal(area.X, Run("FillHalfLeft", area).X);
        Assert.Equal(area.Right, Run("FillHalfRight", area).Right);
    }

    [Theory, MemberData(nameof(Screens))]
    public void FillHalves_MeetWithoutSeamOrOverlap(Rect area) =>
        Assert.Equal(Run("FillHalfLeft", area).Right, Run("FillHalfRight", area).X);

    [Theory, MemberData(nameof(Screens))]
    public void FillHalves_TakeFullHeight(Rect area)
    {
        foreach (var r in new[] { Run("FillHalfLeft", area), Run("FillHalfRight", area) })
        {
            Assert.Equal((area.Y, area.Height), (r.Y, r.Height));
        }
    }

    [Theory, MemberData(nameof(Screens))]
    public void FillHalves_WiderThanFittedHalvesByTwiceGap(Rect area)
    {
        Assert.Equal(2 * Gap(area), Run("FillHalfLeft", area).Width - Run("FitHalfLeft", area).Width);
        Assert.Equal(2 * Gap(area), Run("FillHalfRight", area).Width - Run("FitHalfRight", area).Width);
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitScreen_GapsAreSymmetric(Rect area)
    {
        var r = Run("FitScreen", area);
        Assert.Equal(r.X - area.X, area.Right - r.Right);
        Assert.Equal(r.Y - area.Y, area.Bottom - r.Bottom);
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitScreen_SideGapIsEighteenPercent(Rect area) =>
        Assert.Equal((int)Math.Floor(Reference(area) * 0.18), Run("FitScreen", area).X - area.X);

    [Theory, MemberData(nameof(Screens))]
    public void FitScreen_TopGapIsNinePercent(Rect area) =>
        Assert.Equal((int)Math.Floor(Reference(area) * 0.09), Run("FitScreen", area).Y - area.Y);

    [Theory, MemberData(nameof(Screens))]
    public void FillScreen_IsTheWorkArea(Rect area) =>
        Assert.Equal(area, Run("FillScreen", area));

    [Theory, MemberData(nameof(Screens))]
    public void CenterOnly_KeepsSize(Rect area)
    {
        var r = Run("CenterOnly", area);
        Assert.Equal((Start.Frame.Width, Start.Frame.Height), (r.Width, r.Height));
    }

    [Theory, MemberData(nameof(Screens))]
    public void CenterOnly_CentresOnBothAxes(Rect area)
    {
        var r = Run("CenterOnly", area);
        Assert.InRange(Math.Abs((r.X - area.X) - (area.Right - r.Right)), 0, 1);
        Assert.InRange(Math.Abs((r.Y - area.Y) - (area.Bottom - r.Bottom)), 0, 1);
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitQuarters_OuterGapsAreGap(Rect area)
    {
        var (tl, tr, bl, br) = Quarters("Fit", area);
        var g = Gap(area);
        Assert.Equal((g, g), (tl.X - area.X, tl.Y - area.Y));
        Assert.Equal((g, g), (area.Right - tr.Right, tr.Y - area.Y));
        Assert.Equal((g, g), (bl.X - area.X, area.Bottom - bl.Bottom));
        Assert.Equal((g, g), (area.Right - br.Right, area.Bottom - br.Bottom));
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitQuarters_SeamsAreTwiceGap(Rect area)
    {
        var (tl, tr, bl, br) = Quarters("Fit", area);
        var seam = 2 * Gap(area);
        Assert.Equal((seam, seam), (tr.X - tl.Right, br.X - bl.Right));
        Assert.Equal((seam, seam), (bl.Y - tl.Bottom, br.Y - tr.Bottom));
    }

    [Theory, MemberData(nameof(Screens))]
    public void FitQuarters_ColumnsMatchFittedHalves(Rect area)
    {
        var (tl, tr, bl, br) = Quarters("Fit", area);
        var left = Run("FitHalfLeft", area);
        var right = Run("FitHalfRight", area);
        Assert.All(new[] { tl, bl }, r => Assert.Equal((left.X, left.Width), (r.X, r.Width)));
        Assert.All(new[] { tr, br }, r => Assert.Equal((right.X, right.Width), (r.X, r.Width)));
    }

    [Theory, MemberData(nameof(Screens))]
    public void FillQuarters_TileTheAreaExactly(Rect area)
    {
        var (tl, tr, bl, br) = Quarters("Fill", area);
        Assert.Equal(Rect.FromEdges(area.X, area.Y, tr.X, bl.Y), tl);
        Assert.Equal(Rect.FromEdges(tl.Right, area.Y, area.Right, br.Y), tr);
        Assert.Equal(Rect.FromEdges(area.X, tl.Bottom, br.X, area.Bottom), bl);
        Assert.Equal(Rect.FromEdges(bl.Right, tr.Bottom, area.Right, area.Bottom), br);
    }

    // Math.round semantics: an odd width puts the extra pixel in the left half.
    [Fact]
    public void OddWidth_LeftHalfTakesTheExtraPixel()
    {
        var area = new Rect(0, 0, 1921, 1153);
        Assert.Equal(961, Run("FillHalfLeft", area).Width);
        Assert.Equal(new Rect(961, 0, 960, 1153), Run("FillHalfRight", area));
    }

    // The same rounding on the height puts the extra row in the top quarters.
    [Fact]
    public void OddSize_TopLeftQuarterTakesTheExtraPixels()
    {
        var area = new Rect(0, 0, 1921, 1153);
        Assert.Equal(new Rect(0, 0, 961, 577), Run("FillQuarterTopLeft", area));
        Assert.Equal(new Rect(961, 577, 960, 576), Run("FillQuarterBottomRight", area));
    }

    // A window larger than the area centres with floor towards negative infinity, not truncation.
    [Fact]
    public void CenterOnly_OversizedWindowFloorsTowardsNegativeInfinity()
    {
        var big = Start with { Frame = new Rect(0, 0, 2001, 1201) };
        var r = Actions.ByName("CenterOnly").Plan(new Rect(2560, 0, 1920, 1154), big);
        Assert.Equal(new Rect(2519, -24, 2001, 1201), r);
    }
}
