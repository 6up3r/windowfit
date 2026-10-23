using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using WindowFit.Core;

namespace WindowFit;

/// <summary>Tray icon, the move worker, and the triggers that reinstall the hook.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private static readonly TimeSpan RehookPeriod = TimeSpan.FromMinutes(5);

    private readonly BlockingCollection<WindowAction> _queue = new();
    private readonly KeyboardHook _hook;
    private readonly System.Threading.Timer _rehookTimer;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly TrayText _text = TrayText.For(CultureInfo.CurrentUICulture.Name);
    private readonly Icon _activeIcon = LoadIcon("windowfit.ico");
    private readonly Icon _pausedIcon = LoadIcon("windowfit-paused.ico");

    public TrayContext()
    {
        new Thread(Work) { IsBackground = true, Name = "window-mover" }.Start();
        _hook = new KeyboardHook(action => _queue.TryAdd(action));
        _rehookTimer = new System.Threading.Timer(_ => _hook.Rehook(), null, RehookPeriod, RehookPeriod);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        var menu = new ContextMenuStrip();
#if DEBUG
        menu.Items.Add("Open log", null, (_, _) => OpenLog());
#endif
        _enabledItem = new ToolStripMenuItem(_text.Enabled) { Checked = true, CheckOnClick = true };
        _enabledItem.CheckedChanged += (_, _) => SetEnabled(_enabledItem.Checked);
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_text.Exit, null, (_, _) => ExitThread());
        _icon = new NotifyIcon { Icon = _activeIcon, Text = _text.Tooltip(enabled: true), ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => _enabledItem.Checked = !_enabledItem.Checked;
    }

    private void SetEnabled(bool enabled)
    {
        _hook.Enabled = enabled;
        _icon.Icon = enabled ? _activeIcon : _pausedIcon;
        _icon.Text = _text.Tooltip(enabled);
    }

    private static Icon LoadIcon(string name)
    {
        using var stream = typeof(TrayContext).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"missing resource {name}");
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    protected override void ExitThreadCore()
    {
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        _rehookTimer.Dispose();
        _hook.Dispose();
        _queue.CompleteAdding();
        _icon.Visible = false;
        _icon.Dispose();
        _activeIcon.Dispose();
        _pausedIcon.Dispose();
        Log.Write("stopped");
        base.ExitThreadCore();
    }

    private void Work()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
        {
            try
            {
                WindowMover.Apply(action);
            }
            catch (Exception e)
            {
                Log.Write($"ERROR {action.Name}: {e}");
            }
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            _hook.Rehook();
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            _hook.Rehook();
        }
    }

#if DEBUG
    private static void OpenLog()
    {
        if (File.Exists(Log.FilePath))
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{Log.FilePath}\"") { UseShellExecute = false });
        }
    }
#endif
}
