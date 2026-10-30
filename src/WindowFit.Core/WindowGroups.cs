namespace WindowFit.Core;

/// <summary>What the app sees of a window: its monitor, visible frame, virtual desktop and process, or that it is minimized.</summary>
public readonly record struct WindowProbe(nint Monitor, Rect Frame, bool Minimized, Guid Desktop, uint Process);

/// <summary>
/// Windows tiled together on a monitor, so that activating one brings the others back. A group holds no two tiles
/// in conflict; a placement keeps the window with its companions, else joins the monitor's most recent group, else
/// opens a group, and then merges with the most recent other group when nothing conflicts. Within one Win press only
/// the window's last placement counts. Every public member takes the one lock: the mover and the event thread share it.
/// </summary>
public sealed class WindowGroups
{
    private sealed class Group(nint monitor)
    {
        public nint Monitor { get; } = monitor;
        public Dictionary<nint, (Tile Tile, WindowProbe Placed)> Members { get; } = [];
    }

    private readonly object _lock = new();
    // Most recent first; a monitor's most recent group is its first entry here.
    private List<Group> _recent = [];
    private Dictionary<nint, Group> _of = [];
    // The state before a window's first placement in a Win press; restored when the same press places it again.
    private (nint Window, int Chord, List<Group> State)? _snapshot;

    /// <summary>Same tile, or one tile contains the other; Center only partly covers halves and quarters.</summary>
    public static bool Conflict(Tile a, Tile b) =>
        a == b || a == Tile.Screen || b == Tile.Screen || Contains(a, b) || Contains(b, a);

    private static bool Contains(Tile outer, Tile inner) => (outer, inner) switch
    {
        (Tile.Left, Tile.TopLeft or Tile.BottomLeft) => true,
        (Tile.Right, Tile.TopRight or Tile.BottomRight) => true,
        _ => false,
    };

    /// <summary>
    /// A placement by windowfit, with the window as read back; a null tile (CenterOnly) or Screen takes it out of its
    /// group. The monitor's other members are validated first, so one that left unseen no longer holds its tile.
    /// </summary>
    public void Moved(nint window, Tile? tile, WindowProbe placed, int chord, Func<nint, WindowProbe?> probe)
    {
        lock (_lock)
        {
            if (_snapshot is { } s && s.Window == window && s.Chord == chord)
            {
                Restore(s.State);
            }
            else
            {
                _snapshot = (window, chord, Copy(_recent));
            }
            foreach (var group in _recent.Where(g => g.Monitor == placed.Monitor).ToArray())
            {
                foreach (var (member, record) in group.Members.Where(m => m.Key != window).ToArray())
                {
                    if (!Valid(record.Placed, probe(member)))
                    {
                        Drop(member);
                    }
                }
            }
            Place(window, tile, placed);
        }
    }

    public void Remove(nint window)
    {
        lock (_lock)
        {
            Drop(window);
        }
    }

    /// <summary>A member minimized a moment ago leaves, unless its whole group is minimized (Win+D, Win+M).</summary>
    public void MinimizeSettled(nint window, Func<nint, bool> isMinimized)
    {
        lock (_lock)
        {
            if (_of.TryGetValue(window, out var group) && !group.Members.Keys.All(isMinimized))
            {
                Drop(window);
            }
        }
    }

    /// <summary>
    /// The companions to bring beneath an activated window. Members the probe rejects, or whose monitor, frame,
    /// virtual desktop or process differ from the recorded ones, leave; minimized ones stay but are not returned.
    /// <paramref name="probe"/> runs under the lock and may pump COM messages: a re-entrant call from the same thread
    /// takes the lock again (Monitor is re-entrant), and the group is read again after the loop.
    /// </summary>
    public IReadOnlyList<nint> Activate(nint window, Func<nint, WindowProbe?> probe)
    {
        lock (_lock)
        {
            if (!_of.TryGetValue(window, out var group))
            {
                return [];
            }
            var minimized = new HashSet<nint>();
            foreach (var (member, record) in group.Members.ToArray())
            {
                var now = probe(member);
                if (now is { Minimized: true })
                {
                    minimized.Add(member);
                }
                else if (!Valid(record.Placed, now))
                {
                    Drop(member);
                }
            }
            if (!_of.ContainsKey(window) || group.Members.Count < 2)
            {
                return [];
            }
            Touch(group);
            return group.Members.Keys.Where(m => m != window && !minimized.Contains(m)).ToList();
        }
    }

    /// <summary>The window's group, the window included; empty when it is in none. Used by the tests and by
    /// <c>WindowEvents.Raise</c>'s owner walk.</summary>
    public IReadOnlyList<nint> GroupOf(nint window)
    {
        lock (_lock)
        {
            return _of.TryGetValue(window, out var group) ? group.Members.Keys.ToList() : [];
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _snapshot = null;
            _recent = [];
            _of = [];
        }
    }

    // Minimized counts as valid: the minimize event decides whether it leaves.
    private static bool Valid(WindowProbe placed, WindowProbe? now) =>
        now is { Minimized: true } ||
        now is { } p && p.Monitor == placed.Monitor && p.Frame == placed.Frame && p.Desktop == placed.Desktop && p.Process == placed.Process;

    private void Place(nint window, Tile? tile, WindowProbe placed)
    {
        var monitor = placed.Monitor;
        if (tile is not { } t || t == Tile.Screen)
        {
            Leave(window);
            return;
        }
        Group target;
        if (_of.TryGetValue(window, out var own) && own.Monitor == monitor && Fits(own, t, window))
        {
            // Retiling inside the own group: its recency stays.
            own.Members[window] = (t, placed);
            target = own;
        }
        else
        {
            Leave(window);
            var recent = MostRecent(monitor, except: null);
            if (recent is not null && Fits(recent, t, window))
            {
                recent.Members[window] = (t, placed);
                Touch(recent);
                target = recent;
            }
            else
            {
                target = new Group(monitor);
                target.Members[window] = (t, placed);
                _recent.Insert(0, target);
            }
            _of[window] = target;
        }
        if (MostRecent(monitor, except: target) is { } other &&
            other.Members.Values.All(o => target.Members.Values.All(m => !Conflict(o.Tile, m.Tile))))
        {
            foreach (var (member, record) in other.Members)
            {
                target.Members[member] = record;
                _of[member] = target;
            }
            _recent.Remove(other);
            Touch(target);
        }
    }

    private static bool Fits(Group group, Tile tile, nint window) =>
        group.Members.Where(m => m.Key != window).All(m => !Conflict(m.Value.Tile, tile));

    private Group? MostRecent(nint monitor, Group? except) =>
        _recent.FirstOrDefault(g => g.Monitor == monitor && g != except);

    private void Touch(Group group)
    {
        _recent.Remove(group);
        _recent.Insert(0, group);
    }

    // Out of the groups and out of the chord's snapshot, so a later placement in the same Win press cannot bring it back.
    private void Drop(nint window)
    {
        Leave(window);
        if (_snapshot is { } s)
        {
            foreach (var group in s.State.Where(g => g.Members.Remove(window)).ToArray())
            {
                if (group.Members.Count == 0)
                {
                    s.State.Remove(group);
                }
            }
        }
    }

    private void Leave(nint window)
    {
        if (_of.Remove(window, out var group))
        {
            group.Members.Remove(window);
            if (group.Members.Count == 0)
            {
                _recent.Remove(group);
            }
        }
    }

    private static List<Group> Copy(List<Group> groups) =>
        groups.Select(g =>
        {
            var copy = new Group(g.Monitor);
            foreach (var (member, placed) in g.Members)
            {
                copy.Members[member] = placed;
            }
            return copy;
        }).ToList();

    private void Restore(List<Group> state)
    {
        _recent = Copy(state);
        _of = _recent.SelectMany(g => g.Members.Keys.Select(m => (m, g))).ToDictionary(x => x.m, x => x.g);
    }
}
