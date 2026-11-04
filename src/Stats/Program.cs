using FifthBox.ServerManager.Stats;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<StatsOptions>(builder.Configuration.GetSection("Stats"));
builder.Services.AddSingleton<IDockerStats, DockerStatsClient>();
builder.Services.AddSingleton<StatsCollector>();
builder.Services.AddSingleton<MachineReader>();
builder.Services.AddSingleton<IReportSender>(sp =>
{
    var options = sp.GetRequiredService<IOptions<StatsOptions>>().Value;
    return new HostReportSender(options.HostUrl, File.ReadAllText(options.KeyFile).Trim(), TimeProvider.System);
});
builder.Services.AddHostedService<StatsWorker>();

builder.Build().Run();
