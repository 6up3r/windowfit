using WindowFit.Core;
using static WindowFit.Native;

namespace WindowFit;

/// <summary>
/// WinEvents on their own thread: a window closed, hidden or minimized alone leaves its group, and an activated group
/// member brings its companions directly beneath it without taking the focus. A drag is caught at activation, by the
/// frame no longer matching: a click on a caption also ends a move loop, so its end event proves no move.
/// </summary>
internal sealed class WindowEvents : IDisposable
{
    // Win+D and Win+M minimize every window within this time; a member minimized alone is decided after it.
    private static readonly TimeSpan MinimizeSettle = TimeSpan.FromMilliseconds(400);
    private static readonly uint[] Events =
        [EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_MINIMIZESTART, EVENT_OBJECT_DESTROY, EVENT_OBJECT_HIDE];

    private readonly WindowGroups _groups;
    private readonly Func<bool> _active;
    private readonly WinEventProc _proc;
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private uint _threadId;

    /// <param name="active">True while windowfit is enabled and Groups is ticked: only then activations raise.</param>
    public WindowEvents(WindowGroups groups, Func<bool> active)
    {
        _groups = groups;
        _active = active;
        _proc = OnEvent;
        _thread = new Thread(Run) { IsBackground = true, Name = "window-events" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
        _ready.Wait();
    }

    public void Dispose()
    {
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        var hooks = Events.Select(e => SetWinEventHook(e, e, IntPtr.Zero, _proc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS)).ToList();
        if (hooks.Contains(IntPtr.Zero))
        {
            Log.Write("ERROR SetWinEventHook failed: groups do not follow every window event");
        }
        _ready.Set();
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
        foreach (var hook in hooks.Where(h => h != IntPtr.Zero))
        {
            UnhookWinEvent(hook);
        }
    }

    private void OnEvent(IntPtr hook, uint winEvent, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        if (hwnd == IntPtr.Zero || idObject != OBJID_WINDOW || idChild != CHILDID_SELF)
        {
            return;
        }
        try
        {
            switch (winEvent)
            {
                case EVENT_OBJECT_DESTROY or EVENT_OBJECT_HIDE:
                    _groups.Remove(hwnd);
                    break;
                case EVENT_SYSTEM_MINIMIZESTART:
                    _ = Task.Delay(MinimizeSettle).ContinueWith(_ => _groups.MinimizeSettled(hwnd, h => IsIconic(h)), TaskScheduler.Default);
                    break;
                case EVENT_SYSTEM_FOREGROUND when _active():
                    Raise(hwnd);
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Write($"ERROR window event 0x{winEvent:X}: {e}");
        }
    }

    private void Raise(IntPtr hwnd)
    {
        // A stale or spoofed foreground event reorders nothing.
        if (GetForegroundWindow() != hwnd)
        {
            return;
        }
        // The activated window, or its nearest owner in a group: a Save dialog brings its owner's group.
        var member = hwnd;
        while (member != IntPtr.Zero && _groups.GroupOf(member).Count == 0)
        {
            member = GetWindow(member, GW_OWNER);
        }
        if (member == IntPtr.Zero)
        {
            return;
        }
        // The foreground window itself is never placed beneath anything.
        var companions = _groups.Activate(member, h => WindowMover.Probe(h)).Where(c => c != hwnd).ToList();
        if (companions.Count == 0)
        {
            return;
        }
        var stack = VisibleZOrder();
        var sorted = companions.OrderBy(c => stack.IndexOf(c) is var i and >= 0 ? i : int.MaxValue).ToList();
        var below = stack.SkipWhile(w => w != member).Skip(1).Take(sorted.Count);
        if (below.SequenceEqual(sorted))
        {
            return;
        }
        var previous = member;
        foreach (var companion in sorted)
        {
            // NOOWNERZORDER: a companion's owner outside the group stays where it is.
            if (SetWindowPos(companion, previous, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_NOOWNERZORDER))
            {
                previous = companion;
            }
            else
            {
                Log.Write($"group: cannot place a companion beneath the active window ({System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");
                _groups.Remove(companion);
            }
        }
    }

    // Top-level windows from the top down, the visible ones only.
    private static List<IntPtr> VisibleZOrder()
    {
        var windows = new List<IntPtr>();
        EnumWindows((h, _) =>
        {
            if (IsWindowVisible(h))
            {
                windows.Add(h);
            }
            return true;
        }, IntPtr.Zero);
        return windows;
    }
}
