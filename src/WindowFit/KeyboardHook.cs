using System.Runtime.InteropServices;
using WindowFit.Core;
using static WindowFit.Native;

namespace WindowFit;

/// <summary>
/// WH_KEYBOARD_LL hook on its own thread. The callback only matches the chord, swallows the arrow
/// and queues the action: a callback that exceeds LowLevelHooksTimeout is removed silently, so
/// the hook is also reinstalled on <see cref="Rehook"/>.
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private const uint WM_APP_REHOOK = WM_APP + 1;
    // Tags the events this process injects, so the callback lets them through untouched.
    private static readonly UIntPtr InjectedTag = 0x57464954;

    private readonly Action<WindowAction> _onAction;
    private readonly LowLevelKeyboardProc _proc;
    private readonly ChordTracker _chords = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly Thread _thread;
    private uint _threadId;
    private IntPtr _hook;

    public KeyboardHook(Action<WindowAction> onAction)
    {
        _onAction = onAction;
        _proc = Callback;
        _thread = new Thread(Run) { IsBackground = true, Name = "keyboard-hook", Priority = ThreadPriority.AboveNormal };
        _thread.Start();
        _ready.Wait();
        if (_hook == IntPtr.Zero)
        {
            throw new InvalidOperationException("SetWindowsHookEx failed");
        }
    }

    /// <summary>False lets Win+arrow through to Windows; a rehook keeps the setting.</summary>
    public bool Enabled
    {
        get => _chords.Enabled;
        set => _chords.Enabled = value;
    }

    public void Rehook() => PostThreadMessage(_threadId, WM_APP_REHOOK, IntPtr.Zero, IntPtr.Zero);

    public void Dispose()
    {
        PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        Install();
        _ready.Set();
        while (GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.message == WM_APP_REHOOK)
            {
                UnhookWindowsHookEx(_hook);
                _chords.Reset();
                Install();
            }
        }
        UnhookWindowsHookEx(_hook);
    }

    private void Install()
    {
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            Log.Write($"ERROR SetWindowsHookEx failed ({Marshal.GetLastWin32Error()})");
        }
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
        {
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
        var info = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
        if (info.dwExtraInfo == InjectedTag)
        {
            return CallNextHookEx(_hook, nCode, wParam, lParam);
        }
        var message = (int)wParam;
        if (message is WM_KEYUP or WM_SYSKEYUP && _chords.KeyUp((int)info.vkCode))
        {
            return 1;
        }
        if (message is WM_KEYDOWN or WM_SYSKEYDOWN)
        {
            var verdict = _chords.KeyDown((int)info.vkCode, ReadModifiers());
            if (verdict.Action is not null)
            {
                SendMaskKey();
                _onAction(verdict.Action);
            }
            if (verdict.Swallow)
            {
                return 1;
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    // Modifiers pressed before the arrow; the async state lags only for the key being reported.
    private static Modifiers ReadModifiers() => new(
        Win: IsDown(VK_LWIN) || IsDown(VK_RWIN),
        Ctrl: IsDown(VK_CONTROL),
        Shift: IsDown(VK_SHIFT),
        Alt: IsDown(VK_MENU));

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    // Win pressed and released with nothing in between opens Start; an unassigned key in between prevents it.
    private static void SendMaskKey()
    {
        var inputs = new[] { MaskInput(0), MaskInput(KEYEVENTF_KEYUP) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MaskInput(uint flags) => new()
    {
        type = INPUT_KEYBOARD,
        u = new INPUTUNION { ki = new KEYBDINPUT { wVk = VK_UNASSIGNED_MASK, dwFlags = flags, dwExtraInfo = InjectedTag } },
    };
}
