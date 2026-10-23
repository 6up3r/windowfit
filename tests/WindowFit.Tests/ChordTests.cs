using WindowFit.Core;

namespace WindowFit.Tests;

public class ChordTests
{
    private const int VkLeft = 0x25, VkUp = 0x26, VkRight = 0x27, VkDown = 0x28, VkA = 0x41;

    [Theory]
    [InlineData(VkLeft, false, "FitHalfLeft")]
    [InlineData(VkRight, false, "FitHalfRight")]
    [InlineData(VkUp, false, "FitHalfCenter")]
    [InlineData(VkDown, false, "CenterOnly")]
    [InlineData(VkLeft, true, "FillHalfLeft")]
    [InlineData(VkRight, true, "FillHalfRight")]
    [InlineData(VkUp, true, "FillScreen")]
    [InlineData(VkDown, true, "FitScreen")]
    public void WinArrow_MapsToItsAction(int vk, bool ctrl, string expected) =>
        Assert.Equal(expected, Actions.Match(vk, new Modifiers(Win: true, Ctrl: ctrl, Shift: false, Alt: false))?.Name);

    [Theory]
    [InlineData(VkUp, VkLeft, false, "FitQuarterTopLeft")]
    [InlineData(VkLeft, VkUp, false, "FitQuarterTopLeft")]
    [InlineData(VkUp, VkRight, false, "FitQuarterTopRight")]
    [InlineData(VkRight, VkUp, false, "FitQuarterTopRight")]
    [InlineData(VkDown, VkLeft, false, "FitQuarterBottomLeft")]
    [InlineData(VkLeft, VkDown, false, "FitQuarterBottomLeft")]
    [InlineData(VkDown, VkRight, false, "FitQuarterBottomRight")]
    [InlineData(VkRight, VkDown, false, "FitQuarterBottomRight")]
    [InlineData(VkUp, VkLeft, true, "FillQuarterTopLeft")]
    [InlineData(VkLeft, VkUp, true, "FillQuarterTopLeft")]
    [InlineData(VkUp, VkRight, true, "FillQuarterTopRight")]
    [InlineData(VkRight, VkUp, true, "FillQuarterTopRight")]
    [InlineData(VkDown, VkLeft, true, "FillQuarterBottomLeft")]
    [InlineData(VkLeft, VkDown, true, "FillQuarterBottomLeft")]
    [InlineData(VkDown, VkRight, true, "FillQuarterBottomRight")]
    [InlineData(VkRight, VkDown, true, "FillQuarterBottomRight")]
    public void ArrowWithHeldArrow_MapsToItsQuarter(int held, int vk, bool ctrl, string expected) =>
        Assert.Equal(expected, Actions.Match(vk, new Modifiers(Win: true, Ctrl: ctrl, Shift: false, Alt: false), [held])?.Name);

    [Theory]
    [InlineData(VkUp, VkDown, "CenterOnly")]
    [InlineData(VkDown, VkUp, "FitHalfCenter")]
    [InlineData(VkLeft, VkRight, "FitHalfRight")]
    [InlineData(VkRight, VkLeft, "FitHalfLeft")]
    public void ArrowWithOppositeHeldArrow_MapsToItsOwnAction(int held, int vk, string expected) =>
        Assert.Equal(expected, Actions.Match(vk, new Modifiers(Win: true, Ctrl: false, Shift: false, Alt: false), [held])?.Name);

    [Fact]
    public void ArrowWithBothAxesHeld_PairsWithThePerpendicularOne() =>
        Assert.Equal("FitQuarterBottomLeft", Actions.Match(VkDown, new Modifiers(Win: true, Ctrl: false, Shift: false, Alt: false), [VkUp, VkLeft])?.Name);

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, true, false)]
    [InlineData(false, true, false, false)]
    public void OtherModifierSets_AreNotCaught(bool win, bool ctrl, bool shift, bool alt) =>
        Assert.Null(Actions.Match(VkLeft, new Modifiers(win, ctrl, shift, alt)));

    [Fact]
    public void NonArrowKey_IsNotCaught() =>
        Assert.Null(Actions.Match(VkA, new Modifiers(Win: true, Ctrl: false, Shift: false, Alt: false)));
}
