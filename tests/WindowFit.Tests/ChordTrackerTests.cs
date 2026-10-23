using WindowFit.Core;

namespace WindowFit.Tests;

public class ChordTrackerTests
{
    private const int VkLeft = 0x25, VkUp = 0x26, VkRight = 0x27, VkLWin = 0x5B, VkA = 0x41;
    private static readonly Modifiers Win = new(Win: true, Ctrl: false, Shift: false, Alt: false);
    private static readonly Modifiers None = new(Win: false, Ctrl: false, Shift: false, Alt: false);

    private static ChordTracker WinUpHeld()
    {
        var t = new ChordTracker();
        t.KeyDown(VkLWin, None);
        t.KeyDown(VkUp, Win);
        return t;
    }

    [Fact]
    public void PerpendicularArrow_MovesOnToTheQuarter()
    {
        var t = new ChordTracker();
        t.KeyDown(VkLWin, None);
        Assert.Equal("FitHalfCenter", t.KeyDown(VkUp, Win).Action?.Name);
        Assert.Equal("FitQuarterTopLeft", t.KeyDown(VkLeft, Win).Action?.Name);
    }

    [Fact]
    public void ArrowRepeat_IsSwallowedWithoutAction() =>
        Assert.Equal(new KeyVerdict(true, null), WinUpHeld().KeyDown(VkUp, Win));

    [Fact]
    public void HeldArrow_PairsAgainAfterTheOtherArrowIsReleased()
    {
        var t = WinUpHeld();
        t.KeyDown(VkLeft, Win);
        Assert.True(t.KeyUp(VkLeft));
        Assert.Equal("FitQuarterTopRight", t.KeyDown(VkRight, Win).Action?.Name);
    }

    [Fact]
    public void ArrowFromAnEarlierWinPress_DoesNotPair()
    {
        var t = WinUpHeld();
        Assert.False(t.KeyUp(VkLWin));
        t.KeyDown(VkLWin, None);
        Assert.Equal("FitHalfLeft", t.KeyDown(VkLeft, Win).Action?.Name);
    }

    // The arrow's key-up went to a window or desktop the hook does not see.
    [Fact]
    public void ArrowWithLostKeyUp_RunsAgainOnTheNextChord()
    {
        var t = WinUpHeld();
        t.KeyUp(VkLWin);
        t.KeyDown(VkLWin, None);
        Assert.Equal("FitHalfCenter", t.KeyDown(VkUp, Win).Action?.Name);
    }

    [Fact]
    public void ArrowWithLostKeyUp_PassesThroughWhenPressedAlone()
    {
        var t = WinUpHeld();
        t.KeyUp(VkLWin);
        t.KeyDown(VkA, None);
        t.KeyUp(VkA);
        Assert.Equal(new KeyVerdict(false, null), t.KeyDown(VkUp, None));
        Assert.False(t.KeyUp(VkUp));
    }

    [Fact]
    public void Reset_ForgetsHeldArrows()
    {
        var t = WinUpHeld();
        t.Reset();
        Assert.False(t.KeyUp(VkUp));
    }

    [Fact]
    public void Disabled_PassesEveryChordThrough()
    {
        var t = new ChordTracker { Enabled = false };
        t.KeyDown(VkLWin, None);
        Assert.Equal(new KeyVerdict(false, null), t.KeyDown(VkUp, Win));
        Assert.False(t.KeyUp(VkUp));
    }

    [Fact]
    public void DisabledWhileAnArrowIsHeld_KeepsSwallowingItsRepeatsAndKeyUp()
    {
        var t = WinUpHeld();
        t.Enabled = false;
        Assert.Equal(new KeyVerdict(true, null), t.KeyDown(VkUp, Win));
        Assert.Equal(new KeyVerdict(false, null), t.KeyDown(VkLeft, Win));
        Assert.True(t.KeyUp(VkUp));
    }

    [Fact]
    public void Reset_KeepsDisabled()
    {
        var t = new ChordTracker { Enabled = false };
        t.Reset();
        Assert.False(t.Enabled);
    }

    [Fact]
    public void ReEnabled_MatchesAgain()
    {
        var t = new ChordTracker { Enabled = false };
        t.Enabled = true;
        t.KeyDown(VkLWin, None);
        Assert.Equal("FitHalfCenter", t.KeyDown(VkUp, Win).Action?.Name);
    }
}
