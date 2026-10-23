using System.Diagnostics;
using System.Runtime.InteropServices;
using WindowFit.Core;
using static WindowFit.Native;

namespace WindowFit;

/// <summary>Applies an action to the foreground window on the monitor it sits on.</summary>
internal static class WindowMover
{
    private static readonly HashSet<string> ShellClasses = ["Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd"];

    public static void Apply(WindowAction action)
    {
        var hwnd = GetForegroundWindow();
        var state = ReadState(hwnd);
        if (state is null || action.Plan(WorkArea(hwnd), state) is null)
        {
            Log.Write($"{action.Name}: ignored {Describe(hwnd)}");
            return;
        }
        // The invisible borders and the size differ once restored, so measure after restoring.
        if (IsZoomed(hwnd))
        {
            ShowWindow(hwnd, SW_RESTORE);
            state = ReadState(hwnd);
        }
        var area = WorkArea(hwnd);
        if (state is null || action.Plan(area, state) is not { } target)
        {
            Log.Write($"{action.Name}: ignored after restore {Describe(hwnd)}");
            return;
        }

        GetWindowRect(hwnd, out var wr);
        var place = FrameCompensation.WindowRectFor(target, ToRect(wr), Frame(hwnd));
        var flags = SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOOWNERZORDER | (state.Resizable ? 0 : SWP_NOSIZE);
        if (!SetWindowPos(hwnd, IntPtr.Zero, place.X, place.Y, place.Width, place.Height, flags))
        {
            Log.Write($"ERROR {action.Name}: SetWindowPos failed ({Marshal.GetLastWin32Error()}) {Describe(hwnd)}");
            return;
        }

        var actual = Frame(hwnd);
        if (actual != target)
        {
            Log.Write($"{action.Name}: target {target} landed at {actual} {Describe(hwnd)}");
        }
    }

    private static WindowState? ReadState(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || !IsWindowVisible(hwnd) || IsCloaked(hwnd) || ShellClasses.Contains(ClassName(hwnd)))
        {
            return null;
        }
        var style = (long)GetWindowLongPtr(hwnd, GWL_STYLE);
        var exStyle = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        if ((style & WS_CHILD) != 0)
        {
            return null;
        }
        var frame = Frame(hwnd);
        var fullscreen = !IsZoomed(hwnd) && Covers(frame, MonitorRect(hwnd)) && (style & WS_CAPTION) != WS_CAPTION;
        var movable = !IsIconic(hwnd) && !fullscreen;
        return new WindowState(
            frame,
            Normal: (exStyle & WS_EX_TOOLWINDOW) == 0,
            Movable: movable,
            Resizable: movable && (style & WS_THICKFRAME) != 0);
    }

    private static Rect Frame(IntPtr hwnd)
    {
        if (DwmGetWindowAttribute(hwnd, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT r, Marshal.SizeOf<RECT>()) == 0)
        {
            return ToRect(r);
        }
        GetWindowRect(hwnd, out r);
        return ToRect(r);
    }

    private static bool IsCloaked(IntPtr hwnd) =>
        DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

    private static Rect WorkArea(IntPtr hwnd) => ToRect(MonitorInfo(hwnd).rcWork);

    private static Rect MonitorRect(IntPtr hwnd) => ToRect(MonitorInfo(hwnd).rcMonitor);

    private static MONITORINFO MonitorInfo(IntPtr hwnd)
    {
        var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        GetMonitorInfo(MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST), ref mi);
        return mi;
    }

    private static bool Covers(Rect outer, Rect inner) =>
        outer.X <= inner.X && outer.Y <= inner.Y && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom;

    private static Rect ToRect(RECT r) => Rect.FromEdges(r.Left, r.Top, r.Right, r.Bottom);

    private static string ClassName(IntPtr hwnd)
    {
        var buffer = new char[256];
        return new string(buffer, 0, GetClassName(hwnd, buffer, buffer.Length));
    }

    private static string Describe(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return "(no foreground window)";
        }
        GetWindowThreadProcessId(hwnd, out var pid);
        string process;
        try
        {
            process = Process.GetProcessById((int)pid).ProcessName;
        }
        catch (ArgumentException)
        {
            process = "?";
        }
        return $"[{process} class={ClassName(hwnd)}]";
    }
}
