using System.Diagnostics;
using System.Security.Principal;

namespace WindowFit;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // The Start-menu entry and the setup start the elevated task; this process stays unelevated and must not take the mutex.
        if (args is ["--start"])
        {
            return StartTask();
        }

        Mutex instance;
        try
        {
            instance = new Mutex(initiallyOwned: true, @"Local\windowfit", out var createdNew);
            if (!createdNew)
            {
                return 0;
            }
        }
        catch (UnauthorizedAccessException)
        {
            // An elevated instance owns the mutex: it is already running.
            return 0;
        }

        using (instance)
        {
            ApplicationConfiguration.Initialize();
            Log.Write($"started pid={Environment.ProcessId}" +
                $" elevated={new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)}" +
                $" pmv2={Native.AreDpiAwarenessContextsEqual(Native.GetThreadDpiAwarenessContext(), Native.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)}");
            TrayContext tray;
            try
            {
#if DEBUG
                // A local release folder named in update-source.txt beside the exe, to try an update before a real release.
                var sourceFile = Path.Combine(AppContext.BaseDirectory, "update-source.txt");
                tray = new TrayContext(File.Exists(sourceFile) ? File.ReadAllText(sourceFile).Trim() : null);
#else
                tray = new TrayContext();
#endif
            }
            catch (Exception e)
            {
                Log.Write($"ERROR startup: {e}");
                return 1;
            }
            Application.Run(tray);
        }
        return 0;
    }

    private static int StartTask()
    {
        var schtasks = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
        using var process = Process.Start(new ProcessStartInfo(schtasks, ["/Run", "/TN", @"\windowfit\windowfit"])
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        })!;
        process.WaitForExit();
        return process.ExitCode;
    }
}
