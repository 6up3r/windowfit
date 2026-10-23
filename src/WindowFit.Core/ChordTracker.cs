namespace WindowFit.Core;

/// <summary>What the hook does with a key event: pass it on, swallow it, or swallow it and run Action.</summary>
public readonly record struct KeyVerdict(bool Swallow, WindowAction? Action);

/// <summary>
/// The arrows the hook swallows between their key-down and key-up, and the quarters they pair into.
/// Only the most recently pressed key autorepeats, so a key-down for a held arrow that is not the
/// latest key-down is a new press whose key-up never reached the hook.
/// </summary>
public sealed class ChordTracker
{
    private const int VkLWin = 0x5B, VkRWin = 0x5C;

    // Held arrow -> the Win press it went down in; only arrows of the current press pair.
    private readonly Dictionary<int, int> _held = [];
    private int _latestDown;
    private int _winPress;
    // Set by the tray's UI thread, read by the hook thread.
    private volatile bool _enabled = true;

    /// <summary>False lets every new chord through; an arrow already swallowed stays swallowed until its key-up.</summary>
    public bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public KeyVerdict KeyDown(int virtualKey, Modifiers modifiers)
    {
        var repeat = virtualKey == _latestDown;
        _latestDown = virtualKey;
        if (virtualKey is VkLWin or VkRWin && !repeat)
        {
            _winPress++;
        }
        if (_held.ContainsKey(virtualKey))
        {
            if (repeat)
            {
                return new(true, null);
            }
            _held.Remove(virtualKey);
        }
        if (!_enabled)
        {
            return new(false, null);
        }
        var paired = _held.Where(h => h.Value == _winPress).Select(h => h.Key).ToArray();
        var action = Actions.Match(virtualKey, modifiers, paired);
        if (action is null)
        {
            return new(false, null);
        }
        _held[virtualKey] = _winPress;
        return new(true, action);
    }

    /// <summary>True when the key-up belongs to a swallowed arrow and must be swallowed too.</summary>
    public bool KeyUp(int virtualKey)
    {
        if (virtualKey == _latestDown)
        {
            _latestDown = 0;
        }
        return _held.Remove(virtualKey);
    }

    public void Reset()
    {
        _held.Clear();
        _latestDown = 0;
    }
}
