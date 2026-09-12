using FifthBox.ServerManager.Agent;

// Service registration runs and exits; it never starts the worker.
if (args.Length > 0 && OperatingSystem.IsWindows())
{
    switch (args[0].ToLowerInvariant())
    {
        case "install":
            return ServiceInstaller.Install(Environment.ProcessPath!);
        case "uninstall":
            return ServiceInstaller.Uninstall();
    }
}

var builder = Host.CreateApplicationBuilder(args);

// A no-op unless the service manager started this process, so the same executable still runs as a
// console app when it's double-clicked, and on Linux.
builder.Services.AddWindowsService(options => options.ServiceName = ServiceInstaller.ServiceName);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<ISourceProvider, ZipSourceProvider>();
builder.Services.AddSingleton<ISourceProvider, SteamCmdSourceProvider>();
builder.Services.AddSingleton<WorkloadFiles>();
builder.Services.AddSingleton<MachineMetrics>();
builder.Services.AddSingleton<AgentProcessManager>();
builder.Services.AddHostedService<AgentWorker>();

builder.Build().Run();
return 0;
