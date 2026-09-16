using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Agent;

public sealed class AgentWorker(
    IOptions<AgentOptions> options,
    IHttpClientFactory httpFactory,
    AgentProcessManager processes,
    WorkloadFiles files,
    MachineMetrics machine,
    ILogger<AgentWorker> logger) : BackgroundService
{
    private readonly AgentOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hostUrl = _options.HostUrl.TrimEnd('/');

        // before anything else, or we start duplicates of what's already running
        processes.Restore();

        var credentials = await EnsureEnrolledAsync(hostUrl, stoppingToken);
        if (credentials is null)
        {
            logger.LogError("Agent could not enroll — set Agent:EnrollmentKey and Agent:HostUrl. Exiting.");
            return;
        }

        await using var connection = new HubConnectionBuilder()
            .WithUrl($"{hostUrl}/hubs/agents", o =>
                o.AccessTokenProvider = () => Task.FromResult<string?>($"{credentials.AgentId}:{credentials.Secret}"))
            .WithAutomaticReconnect(new ForeverRetryPolicy())
            .Build();

        connection.On<AgentWorkloadSpec, AgentWorkloadStatus>("Deploy", spec => Task.FromResult(processes.Deploy(spec)));
        connection.On<string, AgentWorkloadStatus>("Stop", name => Task.FromResult(processes.Stop(name)));
        connection.On<string, AgentWorkloadStatus>("GetStatus", name => Task.FromResult(processes.GetStatus(name)));
        connection.On<string, int, IReadOnlyList<WorkloadLogLine>>("GetLogs", (name, tail) => Task.FromResult(processes.GetLogs(name, tail)));
        connection.On<string, string, AgentWorkloadStatus>("SendConsole", (name, text) => Task.FromResult(processes.SendConsole(name, text)));
        connection.On<AgentWorkloadSpec, AgentWorkloadStatus>("Update", spec => Task.FromResult(processes.Update(spec)));

        connection.On<string, string, IReadOnlyList<WorkloadFileEntry>>("ListFiles", (name, path) => Task.FromResult(files.List(name, path)));
        connection.On<string, string, WorkloadFileContent>("ReadFile", (name, path) => Task.FromResult(files.Read(name, path)));
        connection.On<string, string, string, bool>("WriteFile", (name, path, text) =>
        {
            files.Write(name, path, text);
            return Task.FromResult(true);
        });

        // only while someone has the Logs tab open
        connection.On<string>("StartFollowingLogs", name => _following[name] = true);
        connection.On<string>("StopFollowingLogs", name =>
        {
            _following.TryRemove(name, out _);
            _pending.TryRemove(name, out _);
        });

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        connection.Closed += error =>
        {
            if (!AgentRejection.IsRejection(error))
            {
                return Task.CompletedTask;
            }

            _rejected = true;
            stop.Cancel();
            logger.LogError(
                "The Host rejected this agent's credential — it has most likely been removed there. " +
                "Delete '{Path}' and restart to enroll again. Not retrying: the same credential can only be refused.",
                _options.ResolvedCredentialsPath);

            return Task.CompletedTask;
        };

        connection.Closed += error =>
        {
            if (error is not null && !AgentRejection.IsRejection(error))
            {
                logger.LogWarning("Connection closed ({Reason}).", Reason(error));
            }

            return Task.CompletedTask;
        };

        connection.Reconnecting += error =>
        {
            // routine, a Host restart looks like this. no stack trace, it's just noise
            logger.LogWarning("Connection lost ({Reason}); reconnecting.", Reason(error));
            return Task.CompletedTask;
        };

        connection.Reconnected += _ =>
        {
            logger.LogInformation("Reconnected to the Host.");
            return ReconcileAsync(connection, stoppingToken);
        };

        // fire and forget, reporting must never stall the supervisor
        processes.StatusChanged += status => _ = ReportAsync(connection, status, stoppingToken);
        processes.LogLine += QueueLine;

        var flush = FlushLogsLoopAsync(connection, stop.Token);

        await ConnectWithRetryAsync(connection, stoppingToken);
        logger.LogInformation("Agent '{Name}' connected as {AgentId}", AgentName(), credentials.AgentId);

        await ReconcileAsync(connection, stoppingToken);

        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.HeartbeatSeconds));
        using var timer = new PeriodicTimer(interval);
        while (!_rejected && !stoppingToken.IsCancellationRequested && await SafeTickAsync(timer, stop.Token))
        {
            try
            {
                if (connection.State == HubConnectionState.Disconnected)
                {
                    // auto reconnect only covers drops after a start, anything else would sit dead forever
                    logger.LogWarning("Connection is closed; starting it again.");
                    await ConnectWithRetryAsync(connection, stoppingToken);
                    await ReconcileAsync(connection, stoppingToken);
                }
                else if (connection.State == HubConnectionState.Connected)
                {
                    await connection.InvokeAsync("Heartbeat", stoppingToken);
                    await ReportMetricsAsync(connection, stoppingToken);
                }

                await processes.ProbeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // a dropped connection already got logged by Reconnecting
                if (connection.State == HubConnectionState.Connected)
                {
                    logger.LogWarning(ex, "Heartbeat failed");
                }
                else
                {
                    logger.LogDebug(ex, "Heartbeat failed while {State}", connection.State);
                }
            }
        }

        await flush;
    }

    /// Host refused our credential, stop trying
    private volatile bool _rejected;

    private readonly ConcurrentDictionary<string, bool> _following = new();
    private readonly ConcurrentDictionary<string, List<WorkloadLogLine>> _pending = new();

    /// nothing drains the queue while disconnected, so cap it and drop the oldest
    private const int MaxPendingLines = 2000;

    private void QueueLine(string workloadName, WorkloadLogLine line)
    {
        if (!_following.ContainsKey(workloadName))
        {
            return; // nobody watching, ring buffer only
        }

        var batch = _pending.GetOrAdd(workloadName, _ => []);
        lock (batch)
        {
            batch.Add(line);

            if (batch.Count > MaxPendingLines)
            {
                batch.RemoveRange(0, batch.Count - MaxPendingLines);
            }
        }
    }

    /// batched per tick, per-line would flood the hub
    private async Task FlushLogsLoopAsync(HubConnection connection, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        while (!ct.IsCancellationRequested && await SafeTickAsync(timer, ct))
        {
            foreach (var name in _pending.Keys)
            {
                if (!_pending.TryGetValue(name, out var batch))
                {
                    continue;
                }

                WorkloadLogLine[] lines;
                lock (batch)
                {
                    if (batch.Count == 0)
                    {
                        continue;
                    }

                    lines = [.. batch];
                    batch.Clear();
                }

                if (connection.State != HubConnectionState.Connected)
                {
                    continue;
                }

                try
                {
                    await connection.InvokeAsync("ReportLogs", name, lines, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogDebug(ex, "Could not report logs for '{Workload}'", name);
                }
            }
        }
    }

    private static async Task<bool> SafeTickAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// swallowed on purpose, an older Host won't know this call (no version handshake yet)
    private async Task ReportMetricsAsync(HubConnection connection, CancellationToken ct)
    {
        try
        {
            await connection.InvokeAsync("ReportMetrics", machine.Read(), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Could not report metrics");
        }
    }

    private async Task ReportAsync(HubConnection connection, AgentWorkloadStatus status, CancellationToken ct)
    {
        if (connection.State != HubConnectionState.Connected)
        {
            return; // next Reconcile re-syncs anyway
        }

        try
        {
            await connection.InvokeAsync("ReportStatus", status, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Could not report status for '{Workload}'", status.Name);
        }
    }

    private async Task ReconcileAsync(HubConnection connection, CancellationToken ct)
    {
        try
        {
            var desired = await connection.InvokeAsync<IReadOnlyList<AgentWorkloadSpec>>("Reconcile", ct);
            logger.LogInformation("Reconciling against {Count} desired workload(s)", desired.Count);
            processes.Reconcile(desired);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            if (_rejected || connection.State != HubConnectionState.Connected)
            {
                // connection went away underneath it, whatever closed it logs the real reason
                logger.LogDebug(ex, "Reconcile abandoned; the connection is {State}", connection.State);
                return;
            }

            logger.LogError(ex, "Reconcile failed");
        }
    }

    private async Task<AgentCredentials?> EnsureEnrolledAsync(string hostUrl, CancellationToken ct)
    {
        var existing = AgentCredentials.Load(_options.ResolvedCredentialsPath);
        if (existing is not null)
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(_options.EnrollmentKey))
        {
            logger.LogError("No stored credential and no enrollment key configured (Agent:EnrollmentKey).");
            return null;
        }

        var http = httpFactory.CreateClient();
        try
        {
            var response = await http.PostAsJsonAsync($"{hostUrl}/api/agents/enroll", new EnrollAgentRequest
            {
                EnrollmentKey = _options.EnrollmentKey,
                Name = AgentName(),
                Platform = DetectPlatform(),
            }, ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Enrollment rejected: {Status}", response.StatusCode);
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<EnrollAgentResponse>(ct);
            if (result is null)
            {
                return null;
            }

            var credentials = new AgentCredentials { AgentId = result.AgentId, Secret = result.Secret };
            credentials.Save(_options.ResolvedCredentialsPath);
            logger.LogInformation("Enrolled as {AgentId}", credentials.AgentId);
            return credentials;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Enrollment failed reaching {HostUrl}", hostUrl);
            return null;
        }
    }

    private async Task ConnectWithRetryAsync(HubConnection connection, CancellationToken ct)
    {
        var attempt = 0;

        while (!ct.IsCancellationRequested && !_rejected)
        {
            try
            {
                await connection.StartAsync(ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                var delay = ForeverRetryPolicy.DelayFor(attempt++);
                logger.LogWarning("Could not reach the Host ({Reason}); retrying in {Delay}s.",
                    Reason(ex), delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }

    private string AgentName() => string.IsNullOrWhiteSpace(_options.Name) ? Environment.MachineName : _options.Name;

    /// innermost message, the outer ones just restate it
    private static string Reason(Exception? error)
    {
        if (error is null)
        {
            return "no reason given";
        }

        var inner = error;
        while (inner.InnerException is { } next)
        {
            inner = next;
        }

        return inner.Message;
    }

    private static AgentPlatform DetectPlatform() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? AgentPlatform.Windows
        : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? AgentPlatform.Linux
        : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? AgentPlatform.MacOS
        : AgentPlatform.Unknown;
}
