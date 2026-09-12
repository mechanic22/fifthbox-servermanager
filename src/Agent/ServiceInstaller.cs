using System.Diagnostics;

namespace FifthBox.ServerManager.Agent;

/// Registers the agent with the Windows service manager, so the same executable an operator can
/// double-click also survives logoff and reboot.
public static class ServiceInstaller
{
    public const string ServiceName = "FifthBoxAgent";

    public static int Install(string exePath)
    {
        // binPath must be the full path and quoted: the service manager's working directory is
        // System32, so a bare filename resolves to nothing.
        var create = Run("create", ServiceName, "binPath=", $"\"{exePath}\"", "start=", "auto",
            "DisplayName=", "FifthBox ServerManager Agent");
        if (create != 0)
        {
            return create;
        }

        Run("description", ServiceName, "Runs workloads assigned by FifthBox.ServerManager.");

        // Come back from a crash rather than sitting dead until someone notices.
        Run("failure", ServiceName, "reset=", "86400", "actions=", "restart/5000/restart/5000/restart/30000");

        Console.WriteLine($"Installed '{ServiceName}'. Start it with:  sc start {ServiceName}");
        return 0;
    }

    public static int Uninstall()
    {
        Run("stop", ServiceName);
        var delete = Run("delete", ServiceName);
        if (delete == 0)
        {
            Console.WriteLine($"Removed '{ServiceName}'.");
        }

        return delete;
    }

    private static int Run(params string[] args)
    {
        using var process = Process.Start(new ProcessStartInfo("sc.exe")
        {
            UseShellExecute = false,
        }.WithArguments(args));

        process!.WaitForExit();

        // 5 is ERROR_ACCESS_DENIED — by far the likeliest failure, and the raw code explains nothing.
        if (process.ExitCode == 5)
        {
            Console.Error.WriteLine("Access denied — run this from an Administrator command prompt.");
        }

        return process.ExitCode;
    }

    private static ProcessStartInfo WithArguments(this ProcessStartInfo info, IEnumerable<string> args)
    {
        foreach (var arg in args)
        {
            info.ArgumentList.Add(arg);
        }

        return info;
    }
}
