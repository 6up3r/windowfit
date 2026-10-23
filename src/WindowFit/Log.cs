using System.Diagnostics;

namespace WindowFit;

/// <summary>
/// Debug builds only: appends to %LOCALAPPDATA%\windowfit\windowfit.log, keeping one previous file
/// past 1 MB. Release builds compile every call out, because an elevated process writing into a
/// user-writable folder can be redirected there by a junction.
/// </summary>
internal static class Log
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static string FilePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "windowfit", "windowfit.log");

    [Conditional("DEBUG")]
    public static void Write(string message)
    {
        lock (Gate)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                }
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
                // A full disk or a locked file must not stop the hook.
            }
        }
    }
}
