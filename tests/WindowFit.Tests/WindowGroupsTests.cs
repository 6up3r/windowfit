using WindowFit.Core;

namespace WindowFit.Tests;

public class WindowGroupsTests
{
    private const nint M = 100, M2 = 200;
    private const nint W1 = 1, W2 = 2, W3 = 3, W4 = 4, W5 = 5, W6 = 6;
    private static readonly Guid D1 = new("11111111-1111-1111-1111-111111111111"), D2 = new("22222222-2222-2222-2222-222222222222");

    /// <summary>Windows on monitors as windowfit leaves them: each move is its own chord unless one is given.</summary>
    private sealed class Desk
    {
        public readonly WindowGroups Groups = new();
        public readonly Dictionary<nint, WindowProbe> Live = [];
        private int _chord = 1000;

        public void Move(nint w, Tile? tile, nint monitor = M, int? chord = null)
        {
            var placed = new WindowProbe(monitor, new Rect((int)(tile ?? Tile.Screen) * 10 + 1, 0, 100, 100), false, D1, 7);
            Live[w] = placed;
            Groups.Moved(w, tile, placed, chord ?? _chord++, Probe);
        }

        public WindowProbe? Probe(nint w) => Live.TryGetValue(w, out var p) ? p : null;

        public IReadOnlyList<nint> Activate(nint w) => Groups.Activate(w, Probe);

        public nint[] Group(nint w) => Groups.GroupOf(w).Order().ToArray();
    }

    [Theory]
    [InlineData(Tile.Left, Tile.TopLeft, true)]
    [InlineData(Tile.Left, Tile.BottomLeft, true)]
    [InlineData(Tile.Right, Tile.TopRight, true)]
    [InlineData(Tile.Right, Tile.BottomRight, true)]
    [InlineData(Tile.Left, Tile.Left, true)]
    [InlineData(Tile.Screen, Tile.Center, true)]
    [InlineData(Tile.Screen, Tile.TopLeft, true)]
    [InlineData(Tile.Center, Tile.Center, true)]
    [InlineData(Tile.Left, Tile.Right, false)]
    [InlineData(Tile.Center, Tile.Left, false)]
    [InlineData(Tile.Center, Tile.TopRight, false)]
    [InlineData(Tile.TopLeft, Tile.BottomLeft, false)]
    [InlineData(Tile.Left, Tile.TopRight, false)]
    public void Conflict_SameOrContaining(Tile a, Tile b, bool expected)
    {
        Assert.Equal(expected, WindowGroups.Conflict(a, b));
        Assert.Equal(expected, WindowGroups.Conflict(b, a));
    }

    [Fact]
    public void TwoHalves_FormOneGroup()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        Assert.Equal([W1, W2], d.Group(W1));
    }

    [Fact]
    public void TakenTile_OpensNewGroup_OldStays()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W3, Tile.Left);
        Assert.Equal([W3], d.Group(W3));
        Assert.Equal([W1, W2], d.Group(W1));
        d.Move(W4, Tile.Right);
        Assert.Equal([W3, W4], d.Group(W4));
        Assert.Equal([W1, W2], d.Group(W1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SonnysExample_EndsInOneGroupOfThree(bool downFirst)
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W3, Tile.Left);
        d.Move(W3, Tile.TopLeft);
        // Browser's quarter: Left then Down, or Down (CenterOnly) then Left, in one Win press.
        d.Move(W1, downFirst ? null : Tile.Left, chord: 7);
        d.Move(W1, Tile.BottomLeft, chord: 7);
        Assert.Equal([W1, W2, W3], d.Group(W1));
    }

    [Fact]
    public void QuarterChord_DownFirst_KeepsTheCompanions()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W3, Tile.Right);
        // Down's CenterOnly alone would take W1 out of {W1, W2} and the quarter would land it in W3's group.
        d.Move(W1, null, chord: 7);
        d.Move(W1, Tile.BottomLeft, chord: 7);
        Assert.Equal([W1, W2], d.Group(W1));
        Assert.Equal([W3], d.Group(W3));
    }

    [Fact]
    public void LeftCenterRight_OneGroup()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Center);
        d.Move(W3, Tile.Right);
        Assert.Equal([W1, W2, W3], d.Group(W2));
    }

    [Fact]
    public void RetileInsideGroup_DoesNotMakeItMostRecent()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W3, Tile.Left);
        d.Move(W2, Tile.TopRight);
        d.Move(W4, Tile.BottomRight);
        Assert.Equal([W3, W4], d.Group(W4));
        Assert.Equal([W1, W2], d.Group(W1));
    }

    [Fact]
    public void OnlyTheMostRecentGroupIsJoined()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W3, Tile.Left);
        d.Move(W4, Tile.Right);
        d.Move(W5, Tile.Center);
        d.Move(W6, Tile.Center);
        Assert.Equal([W3, W4, W5], d.Group(W5));
        Assert.Equal([W6], d.Group(W6));
        Assert.Equal([W1, W2], d.Group(W1));
    }

    [Fact]
    public void ThirtyWindows_NoGroupAboveFive()
    {
        var d = new Desk();
        Tile[] cycle = [Tile.Left, Tile.Right, Tile.Center, Tile.TopLeft, Tile.BottomRight, Tile.TopRight, Tile.BottomLeft];
        for (var w = 1; w <= 30; w++)
        {
            d.Move(w, cycle[w % cycle.Length]);
        }
        Assert.All(Enumerable.Range(1, 30), w => Assert.InRange(d.Group(w).Length, 1, 5));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Tile.Screen)]
    public void CenterOnlyOrScreen_LeavesTheGroup(Tile? tile)
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W2, tile);
        Assert.Equal([W1], d.Group(W1));
        Assert.Empty(d.Group(W2));
    }

    [Fact]
    public void ChordRollback_DoesNotCrossChords()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W3, Tile.Left);
        d.Move(W1, null, chord: 7);
        d.Move(W1, Tile.BottomLeft, chord: 8);
        Assert.Equal([W1], d.Group(W1));
    }

    [Fact]
    public void ChordRollback_OnlyForTheSameWindow()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left, chord: 7);
        d.Move(W2, Tile.Right, chord: 7);
        Assert.Equal([W1, W2], d.Group(W1));
    }

    [Fact]
    public void Clear_DiscardsTheChordSnapshot()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W1, Tile.TopLeft, chord: 3);
        d.Groups.Clear();
        // A snapshot surviving Clear would bring W2 back into W1's group.
        d.Move(W1, Tile.BottomLeft, chord: 3);
        Assert.Equal([W1], d.Group(W1));
        Assert.Empty(d.Group(W2));
    }

    [Fact]
    public void Activate_ReturnsCompanions_AndMarksMostRecent()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W3, Tile.Left);
        Assert.Equal([W2], d.Activate(W1));
        d.Move(W4, Tile.Center);
        Assert.Equal([W1, W2, W4], d.Group(W4));
    }

    [Fact]
    public void Activate_SameGroupAlreadyRecent_StaysRecent()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        Assert.Equal([W2], d.Activate(W1));
        Assert.Equal([W1], d.Activate(W2));
        d.Move(W3, Tile.Center);
        Assert.Equal([W1, W2, W3], d.Group(W3));
    }

    [Fact]
    public void Activate_KeepsMemberAtRecordedFrame()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        Assert.Equal([W2], d.Activate(W1));
        Assert.Equal([W1, W2], d.Group(W1));
    }

    [Fact]
    public void Activate_DropsMovedMember()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Live[W2] = d.Live[W2] with { Frame = new Rect(5, 5, 100, 100) };
        Assert.Empty(d.Activate(W1));
        Assert.Empty(d.Group(W2));
        Assert.Equal([W1], d.Group(W1));
    }

    [Fact]
    public void Activate_DropsMemberOnOtherMonitor()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Live[W2] = d.Live[W2] with { Monitor = M2 };
        Assert.Empty(d.Activate(W1));
        Assert.Empty(d.Group(W2));
    }

    [Fact]
    public void Activate_DropsMembersTheProbeRejects()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Center);
        d.Move(W3, Tile.Right);
        d.Live.Remove(W2);
        Assert.Equal([W3], d.Activate(W1));
        Assert.Equal([W1, W3], d.Group(W1));
    }

    [Fact]
    public void Activate_SkipsMinimizedButKeepsThem()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Center);
        d.Move(W3, Tile.Right);
        d.Live[W2] = d.Live[W2] with { Minimized = true, Frame = default };
        Assert.Equal([W3], d.Activate(W1));
        Assert.Equal([W1, W2, W3], d.Group(W1));
    }

    [Fact]
    public void Activate_ActivatedWindowItselfMoved_ReturnsEmpty()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Live[W1] = d.Live[W1] with { Frame = new Rect(0, 0, 50, 50) };
        Assert.Empty(d.Activate(W1));
        Assert.Empty(d.Group(W1));
        Assert.Equal([W2], d.Group(W2));
    }

    [Fact]
    public void Activate_WindowInNoGroup_ReturnsEmpty() => Assert.Empty(new Desk().Activate(W1));

    [Fact]
    public void MinimizeSettled_AloneLeaves_AllTogetherStay()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Groups.MinimizeSettled(W1, _ => true);
        Assert.Equal([W1, W2], d.Group(W1));
        d.Groups.MinimizeSettled(W1, w => w == W1);
        Assert.Empty(d.Group(W1));
        Assert.Equal([W2], d.Group(W2));
    }

    [Fact]
    public void Remove_ForgetsTheWindow()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Groups.Remove(W2);
        Assert.Empty(d.Activate(W1));
        Assert.Empty(d.Group(W2));
    }

    [Fact]
    public void Monitors_KeepSeparateGroups()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left, M);
        d.Move(W2, Tile.Right, M2);
        Assert.Equal([W1], d.Group(W1));
        Assert.Equal([W2], d.Group(W2));
    }

    [Fact]
    public void Remove_UnrelatedWindowBetweenChordMoves_KeepsTheRollback()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W3, Tile.Right);
        d.Move(W1, null, chord: 7);
        // A tooltip hidden by the relayout of the first arrow's move.
        d.Groups.Remove(99);
        d.Move(W1, Tile.BottomLeft, chord: 7);
        Assert.Equal([W1, W2], d.Group(W1));
    }

    [Fact]
    public void Remove_MemberBetweenChordMoves_IsNotRestored()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        d.Move(W1, null, chord: 7);
        d.Groups.Remove(W2);
        d.Move(W1, Tile.BottomLeft, chord: 7);
        Assert.Equal([W1], d.Group(W1));
        Assert.Empty(d.Group(W2));
    }

    [Fact]
    public void Moved_DropsSilentLeaversBeforePlacing()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        // W1 went to another monitor with Win+Shift+arrow, which windowfit does not see.
        d.Live[W1] = d.Live[W1] with { Monitor = M2 };
        d.Move(W3, Tile.Left);
        Assert.Equal([W2, W3], d.Group(W3));
        Assert.Empty(d.Group(W1));
    }

    [Fact]
    public void Activate_DropsMemberOnOtherDesktop()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Center);
        d.Move(W3, Tile.Right);
        d.Live[W2] = d.Live[W2] with { Desktop = D2 };
        Assert.Equal([W3], d.Activate(W1));
        Assert.Empty(d.Group(W2));
    }

    [Fact]
    public void Activate_ActivatedWindowOnOtherDesktop_LeavesTheOthersGrouped()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Center);
        d.Move(W3, Tile.Right);
        d.Live[W1] = d.Live[W1] with { Desktop = D2 };
        Assert.Empty(d.Activate(W1));
        Assert.Empty(d.Group(W1));
        Assert.Equal([W2, W3], d.Group(W2));
    }

    [Fact]
    public void Activate_DropsMemberOfAnotherProcess()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Right);
        // W2's handle now belongs to a new window of another process.
        d.Live[W2] = d.Live[W2] with { Process = 8 };
        Assert.Empty(d.Activate(W1));
        Assert.Empty(d.Group(W2));
    }

    [Fact]
    public void NewWindowAlone_MergesWithTheMostRecentOtherGroup()
    {
        var d = new Desk();
        d.Move(W1, Tile.Left);
        d.Move(W2, Tile.Left);
        d.Move(W2, Tile.TopLeft);
        d.Move(W1, Tile.BottomLeft);
        Assert.Equal([W1, W2], d.Group(W1));
    }
}
