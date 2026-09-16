using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FifthBox.ServerManager.Agent;

/// .net has no cross-platform SIGTERM, so libc on unix and a window close on windows
public static class ProcessSignal
{
    private const int Sigterm = 15;

    // not LibraryImport, it needs AllowUnsafeBlocks project-wide for one call
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int SendSignal(int pid, int signal);

    /// false means it can't be asked nicely, escalate to a kill
    public static bool RequestTerminate(Process process)
    {
        try
        {
            return OperatingSystem.IsWindows()
                ? process.CloseMainWindow()
                : SendSignal(process.Id, Sigterm) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
