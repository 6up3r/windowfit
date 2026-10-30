using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using WindowFit.Core;
using WindowFit.Core.Updates;

namespace WindowFit;

/// <summary>Tray icon, the move worker, the triggers that reinstall the hook, and the update checks.</summary>
internal sealed class TrayContext : ApplicationContext
{
    private static readonly TimeSpan RehookPeriod = TimeSpan.FromMinutes(5);
    // The network may not be up at logon.
    private static readonly TimeSpan FirstCheck = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CheckPeriod = TimeSpan.FromHours(24);
    // A stalled download would otherwise keep the update item disabled until a restart.
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(10);

    private readonly BlockingCollection<(WindowAction Action, int Chord)> _queue = new();
    private readonly KeyboardHook _hook;
    private readonly System.Threading.Timer _rehookTimer;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _groupsItem;
    private readonly WindowGroups _groups = new();
    private readonly WindowEvents _events;
    // Read by the move worker and the event thread.
    private volatile bool _groupsOn = UserSettings.Groups;
    private readonly TrayText _text = TrayText.For(CultureInfo.CurrentUICulture.Name);
    private readonly Icon _activeIcon = LoadIcon("windowfit.ico");
    private readonly Icon _pausedIcon = LoadIcon("windowfit-paused.ico");
    private LicenseWindow? _licenseWindow;
    private NotesWindow? _notesWindow;
    private readonly Updater _updater;
    private readonly System.Threading.Timer _checkTimer;
    private readonly ToolStripMenuItem _updateItem;
    private readonly SynchronizationContext _ui;
    private UpdateOffer? _offer;
    private Version? _announced;
    private bool _checking;
    private bool _installing;

    public TrayContext(string? updateSource = null)
    {
        new Thread(Work) { IsBackground = true, Name = "window-mover" }.Start();
        _hook = new KeyboardHook((action, chord) => _queue.TryAdd((action, chord)));
        _rehookTimer = new System.Threading.Timer(_ => _hook.Rehook(), null, RehookPeriod, RehookPeriod);
        _events = new WindowEvents(_groups, () => _hook.Enabled && _groupsOn);
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;

        var menu = new ContextMenuStrip();
        // The first control installs the WinForms context: checks and timers post their UI work to it.
        _ui = SynchronizationContext.Current ?? throw new InvalidOperationException("no UI synchronization context");
        _updateItem = new ToolStripMenuItem { Visible = false };
        _updateItem.Click += (_, _) => ShowOffer();
        menu.Items.Add(_updateItem);
#if DEBUG
        menu.Items.Add("Open log", null, (_, _) => OpenLog());
#endif
        _enabledItem = new ToolStripMenuItem(_text.Enabled) { Checked = true, CheckOnClick = true };
        _enabledItem.CheckedChanged += (_, _) => SetEnabled(_enabledItem.Checked);
        menu.Items.Add(_enabledItem);
        _groupsItem = new ToolStripMenuItem(_text.Groups) { Checked = _groupsOn, CheckOnClick = true };
        _groupsItem.CheckedChanged += (_, _) => SetGroups(_groupsItem.Checked);
        menu.Items.Add(_groupsItem);
        menu.Items.Add(_text.WhatsNewItem, null, (_, _) => ShowAllNotes());
        menu.Items.Add(_text.CheckForUpdates, null, (_, _) => _ = CheckForUpdate(manual: true));
        menu.Items.Add(_text.LicenseItem, null, (_, _) => ShowLicense());
        menu.Items.Add(_text.Exit, null, (_, _) => ExitThread());
        _icon = new NotifyIcon { Icon = _activeIcon, Text = _text.Tooltip(enabled: true), ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => _enabledItem.Checked = !_enabledItem.Checked;
        _icon.BalloonTipClicked += (_, _) => ShowOffer();

        Updater.CleanUp();
        _updater = new Updater(updateSource);
        _checkTimer = new System.Threading.Timer(_ => _ui.Post(_ => _ = CheckForUpdate(manual: false), null), null, FirstCheck, CheckPeriod);
        _ui.Post(_ => ShowWhatsNewOnce(), null);
    }

    private async Task CheckForUpdate(bool manual)
    {
        if (_checking)
        {
            return;
        }
        _checking = true;
        try
        {
            using var timeout = new CancellationTokenSource(CheckTimeout);
            var offer = await _updater.CheckAsync(timeout.Token);
            if (offer is not null)
            {
                Offered(offer);
                if (manual)
                {
                    ShowOffer();
                }
            }
            else if (manual)
            {
                MessageBox.Show(_text.UpToDate, "windowfit", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception e)
        {
            // An automatic check fails silently and runs again at the next tick.
            Log.Write($"update check failed: {e}");
            if (manual)
            {
                MessageBox.Show(string.Format(_text.CheckFailed, ReleaseCheck.Reason(e)), "windowfit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            _checking = false;
        }
    }

    private void Offered(UpdateOffer offer)
    {
        _offer = offer;
        _updateItem.Text = _text.InstallUpdateItem(offer.Version);
        _updateItem.Visible = true;
        if (_announced != offer.Version)
        {
            _announced = offer.Version;
            _icon.ShowBalloonTip(10_000, "windowfit", _text.Available(offer.Version), ToolTipIcon.Info);
        }
    }

    // An unelevated windowfit cannot run the setup: it opens the release page, which an elevated one never does.
    private void ShowOffer()
    {
        if (_offer is not { } offer)
        {
            return;
        }
        if (!Updater.IsElevated)
        {
            Process.Start(new ProcessStartInfo(offer.Page) { UseShellExecute = true });
            return;
        }
        var title = _text.InstallUpdateItem(offer.Version).TrimEnd('.');
        ShowNotes(title, offer.Notes, Updater.CanInstall ? _text.Install : null).InstallClicked += (_, _) => _ = Install(offer);
    }

    private async Task Install(UpdateOffer offer)
    {
        if (_installing)
        {
            return;
        }
        _installing = true;
        _updateItem.Enabled = false;
        try
        {
            using var timeout = new CancellationTokenSource(InstallTimeout);
            await _updater.InstallAsync(offer, timeout.Token);
            // The setup ends every windowfit before it copies, and starts the new one when it is done.
            ExitThread();
        }
        catch (Exception e)
        {
            Log.Write($"update not installed: {e}");
            MessageBox.Show(string.Format(_text.InstallFailed, ReleaseCheck.Reason(e)), "windowfit", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _updateItem.Enabled = true;
        }
        finally
        {
            _installing = false;
        }
    }

    // A build without a version (0.0.0) neither shows nor records: it would hide the installed release's notes.
    private void ShowWhatsNewOnce()
    {
        var current = Updater.Current;
        if (!ReleaseCheck.IsRelease(current))
        {
            return;
        }
        var seen = UserSettings.SeenVersion;
        var unread = Changelog.Since(Changelog.Parse(LicenseWindow.ReadResource("CHANGELOG.md")), seen, current);
        if (unread.Count > 0)
        {
            ShowNotes(_text.WhatsNew, Changelog.Text(unread), null);
        }
        if (seen is null || ReleaseCheck.Normalize(seen) < ReleaseCheck.Normalize(current))
        {
            UserSettings.SetSeenVersion(current);
        }
    }

    private void ShowAllNotes()
    {
        var current = ReleaseCheck.Normalize(Updater.Current);
        var entries = Changelog.Parse(LicenseWindow.ReadResource("CHANGELOG.md"))
            .Where(e => !ReleaseCheck.IsRelease(current) || e.Version <= current);
        ShowNotes(_text.WhatsNew, Changelog.Text(entries), null);
    }

    private NotesWindow ShowNotes(string title, string text, string? installLabel)
    {
        _notesWindow?.Close();
        // Later only beside Install; notes alone close with OK, the same word in every Windows language.
        var window = new NotesWindow(title, text, _activeIcon, installLabel is null ? "OK" : _text.Later, installLabel);
        window.FormClosed += (_, _) =>
        {
            if (_notesWindow == window)
            {
                _notesWindow = null;
            }
        };
        _notesWindow = window;
        window.Show();
        window.Activate();
        return window;
    }

    private void SetEnabled(bool enabled)
    {
        _hook.Enabled = enabled;
        _icon.Icon = enabled ? _activeIcon : _pausedIcon;
        _icon.Text = _text.Tooltip(enabled);
    }

    // Off dissolves every group; on starts from none.
    private void SetGroups(bool on)
    {
        _groupsOn = on;
        _groups.Clear();
        UserSettings.SetGroups(on);
    }

    private void ShowLicense()
    {
        if (_licenseWindow is null)
        {
            _licenseWindow = new LicenseWindow(_text, _activeIcon);
            _licenseWindow.FormClosed += (_, _) => _licenseWindow = null;
            _licenseWindow.Show();
        }
        _licenseWindow.Activate();
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
        _licenseWindow?.Close();
        _notesWindow?.Close();
        _checkTimer.Dispose();
        _updater.Dispose();
        _events.Dispose();
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
        foreach (var (action, chord) in _queue.GetConsumingEnumerable())
        {
            try
            {
                WindowMover.Apply(action, chord, _groupsOn ? _groups : null);
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
