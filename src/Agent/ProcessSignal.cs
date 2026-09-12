using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FifthBox.ServerManager.Agent;

/// Asks a process to shut down rather than killing it. .NET has no cross-platform "send SIGTERM", so
/// Unix goes through libc and Windows uses the console/window close message.
public static class ProcessSignal
{
    private const int Sigterm = 15;

    // DllImport rather than LibraryImport: the source generator needs AllowUnsafeBlocks, which isn't
    // worth turning on project-wide for one two-int call.
    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int SendSignal(int pid, int signal);

    /// False means the process can't be asked politely (no window on Windows, or the signal failed) —
    /// the caller should escalate to a kill.
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
