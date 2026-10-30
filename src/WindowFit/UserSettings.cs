using System.Text;
using static WindowFit.Native;

namespace WindowFit;

/// <summary>
/// The user's choices in HKCU\Software\windowfit: the tray's Groups choice (a DWORD, on when missing) and the version
/// whose "What's new" was shown (a string). The process is elevated and the user can turn the key into a registry
/// link, so the key is opened as a link and a link is never written through.
/// </summary>
internal static class UserSettings
{
    private const string Key = @"Software\windowfit";

    public static bool Groups => ReadDword(Key, "Groups") is not 0;

    public static bool SetGroups(bool on) => Write(Key, "Groups", REG_DWORD, BitConverter.GetBytes(on ? 1u : 0u));

    public static Version? SeenVersion => Version.TryParse(ReadString(Key, "SeenVersion"), out var v) ? v : null;

    public static bool SetSeenVersion(Version version) =>
        Write(Key, "SeenVersion", REG_SZ, Encoding.Unicode.GetBytes(version.ToString(3) + "\0"));

    /// <summary>Null when the key or the value is missing, of another type, or the key is a link.</summary>
    private static uint? ReadDword(string subKey, string name)
    {
        var data = Read(subKey, name, REG_DWORD);
        return data is { Length: 4 } ? BitConverter.ToUInt32(data) : null;
    }

    private static string? ReadString(string subKey, string name) =>
        Read(subKey, name, REG_SZ) is { } data ? Encoding.Unicode.GetString(data).TrimEnd('\0') : null;

    private static byte[]? Read(string subKey, string name, uint expectedType)
    {
        if (RegOpenKeyExW(HKEY_CURRENT_USER, subKey, REG_OPTION_OPEN_LINK, KEY_QUERY_VALUE, out var key) != 0)
        {
            return null;
        }
        try
        {
            var size = 0u;
            if (IsLink(key) || RegQueryValueExW(key, name, IntPtr.Zero, out var type, null, ref size) != 0 || type != expectedType
                || size > 1024)
            {
                return null;
            }
            var data = new byte[size];
            return RegQueryValueExW(key, name, IntPtr.Zero, out type, data, ref size) == 0 && type == expectedType ? data[..(int)size] : null;
        }
        finally
        {
            RegCloseKey(key);
        }
    }

    /// <summary>False when the key is a link or cannot be written.</summary>
    private static bool Write(string subKey, string name, uint type, byte[] data)
    {
        if (RegOpenKeyExW(HKEY_CURRENT_USER, subKey, REG_OPTION_OPEN_LINK, KEY_QUERY_VALUE, out var key) == 0)
        {
            RegCloseKey(key);
        }
        else if (RegCreateKeyExW(HKEY_CURRENT_USER, subKey, 0, null, 0, KEY_QUERY_VALUE, IntPtr.Zero, out key, out _) == 0)
        {
            RegCloseKey(key);
        }
        // Reopened as a link, so a link planted at any moment before is seen and its target is never touched.
        if (RegOpenKeyExW(HKEY_CURRENT_USER, subKey, REG_OPTION_OPEN_LINK, KEY_QUERY_VALUE | KEY_SET_VALUE, out key) != 0)
        {
            return false;
        }
        try
        {
            if (IsLink(key))
            {
                Log.Write($@"refused to write {name}: HKCU\{subKey} is a registry link");
                return false;
            }
            return RegSetValueExW(key, name, 0, type, data, (uint)data.Length) == 0;
        }
        finally
        {
            RegCloseKey(key);
        }
    }

    private static bool IsLink(IntPtr key)
    {
        var size = 0u;
        return RegQueryValueExW(key, "SymbolicLinkValue", IntPtr.Zero, out _, null, ref size) == 0;
    }
}
