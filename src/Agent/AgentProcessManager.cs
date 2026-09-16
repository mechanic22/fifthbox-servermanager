using System.Collections.Concurrent;
using System.Diagnostics;
using FifthBox.ServerManager.Shared.Agents;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.Options;

namespace FifthBox.ServerManager.Agent;

/// a failed start comes back as Running=false + Detail, never thrown
public sealed class AgentProcessManager(
    IOptions<AgentOptions> options,
    IEnumerable<ISourceProvider> sources,
    ILogger<AgentProcessManager> logger)
{
    private readonly ConcurrentDictionary<string, SupervisedWorkload> _workloads = new();
    private readonly string _statePath = options.Value.ResolvedStatePath;
    private readonly string _root = options.Value.ResolvedRootPath;

    public event Action<string, WorkloadLogLine>? LogLine;

    /// raised outside the workload lock so a slow handler can't stall supervision
    public event Action<AgentWorkloadStatus>? StatusChanged;

    private void Announce(AgentWorkloadStatus status)
    {
        try
        {
            StatusChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Status subscriber threw for '{Workload}'", status.Name);
        }
    }

    /// call once before connecting or the first Deploy starts a second copy
    public void Restore()
    {
        foreach (var tracked in AgentState.Load(_statePath).Workloads)
        {
            if (tracked.Spec is null || FindLiveProcess(tracked) is not { } process)
            {
                logger.LogInformation("Dropping stale process-table entry for '{Workload}'", tracked.Name);
                continue;
            }

            var workload = new SupervisedWorkload
            {
                Spec = tracked.Spec,
                // old state files have no hash but hashing the spec gives the same value
                ConfigHash = tracked.ConfigHash ?? ProcessAdoption.HashOf(tracked.Spec),
                Process = process,
                StartedAt = tracked.StartedAtUtc,
                Adopted = true,
            };

            _workloads[tracked.Name] = workload;
            Watch(tracked.Name, workload, process);
            logger.LogInformation("Adopted '{Workload}' already running as PID {Pid}", tracked.Name, process.Id);
        }

        Persist();
    }

    public AgentWorkloadStatus Deploy(AgentWorkloadSpec spec)
    {
        // adopted and on the same config, leave it alone. no bouncing a healthy server on agent restart
        if (_workloads.TryGetValue(spec.Name, out var existing))
        {
            lock (existing.Gate)
            {
                if (existing.Updating)
                {
                    return StatusOf(spec.Name, existing) with { Detail = "an update is running — try again when it finishes" };
                }

                if (existing.Adopted && existing.Process is { } running && IsAlive(running) && existing.ConfigHash == ProcessAdoption.HashOf(spec))
                {
                    // restored spec has no env, take this one or a crash-restart loses it
                    existing.Spec = spec;

                    logger.LogInformation("'{Workload}' already running as PID {Pid} on this config — attached", spec.Name, running.Id);
                    var attached = StatusOf(spec.Name, existing) with { Detail = $"attached to running process (PID {running.Id})" };
                    Announce(attached);
                    return attached;
                }
            }
        }

        Stop(spec.Name);

        var workload = new SupervisedWorkload { Spec = spec, ConfigHash = ProcessAdoption.HashOf(spec) };
        _workloads[spec.Name] = workload;

        AgentWorkloadStatus status;
        lock (workload.Gate)
        {
            Start(spec.Name, workload);
            Persist();
            status = StatusOf(spec.Name, workload);
        }

        Announce(status);
        return status;
    }

    public AgentWorkloadStatus Stop(string name)
    {
        if (!_workloads.TryRemove(name, out var workload))
        {
            return new AgentWorkloadStatus { Name = name, Running = false };
        }

        lock (workload.Gate)
        {
            workload.StopRequested = true;
            var process = workload.Process;
            var input = workload.Input;
            workload.Process = null;
            workload.Input = null;

            if (process is not null)
            {
                Terminate(process, input, workload.Spec);
                process.Dispose();
            }
        }

        Persist();
        var stopped = new AgentWorkloadStatus { Name = name, Running = false, Detail = "stopped" };
        Announce(stopped);
        return stopped;
    }

    public IReadOnlyCollection<string> TrackedNames() => [.. _workloads.Keys];

    public void Reconcile(IReadOnlyList<AgentWorkloadSpec> desired)
    {
        var wanted = desired.Select(s => s.Name).ToHashSet(StringComparer.Ordinal);

        foreach (var name in TrackedNames().Where(n => !wanted.Contains(n)))
        {
            // an update in flight owns the directory, stopping now orphans the download
            if (_workloads.TryGetValue(name, out var tracked) && tracked.Updating)
            {
                logger.LogInformation("Reconcile: leaving '{Workload}' alone while its update runs", name);
                continue;
            }

            logger.LogInformation("Reconcile: stopping '{Workload}' — no longer in desired state", name);
            Stop(name);
        }

        foreach (var spec in desired)
        {
            // no-ops if an adopted process is already on this config
            var status = Deploy(spec);
            if (!status.Running)
            {
                logger.LogWarning("Reconcile: '{Workload}' is not running ({Detail})", spec.Name, status.Detail);
            }
        }
    }

    /// comes back not-running with a reason if there's nothing to write to
    public AgentWorkloadStatus SendConsole(string name, string text)
    {
        if (!_workloads.TryGetValue(name, out var workload))
        {
            return new AgentWorkloadStatus { Name = name, Running = false, Detail = "not running here" };
        }

        lock (workload.Gate)
        {
            var status = StatusOf(name, workload);
            if (!status.Running)
            {
                return status;
            }

            if (workload.Input is not { } input)
            {
                return status with { Running = false, Detail = "console input isn't available for an adopted process" };
            }

            try
            {
                input.WriteLine(text);
                input.Flush();
                return status;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not write to '{Workload}' stdin", name);
                return status with { Running = false, Detail = ex.Message };
            }
        }
    }

    /// returns once the acquire is under way, progress and the finish come through StatusChanged and LogLine
    public AgentWorkloadStatus Update(AgentWorkloadSpec spec)
    {
        if (spec.Source is not { } config || sources.FirstOrDefault(s => s.Kind == config.Kind) is not { } source)
        {
            return new AgentWorkloadStatus { Name = spec.Name, Running = false, Detail = "this agent has no provider for that source" };
        }

        if (!InstallPaths.TryRootFor(_root, spec.Name, out var installRoot))
        {
            return new AgentWorkloadStatus { Name = spec.Name, Running = false, Detail = $"'{spec.Name}' can't be used as a directory name" };
        }

        var workload = _workloads.GetOrAdd(spec.Name, _ => new SupervisedWorkload { Spec = spec, ConfigHash = ProcessAdoption.HashOf(spec) });

        AgentWorkloadStatus started;
        lock (workload.Gate)
        {
            if (workload.Updating)
            {
                return StatusOf(spec.Name, workload) with { Detail = "an update is already running" };
            }

            if (workload.Process is { } running && IsAlive(running))
            {
                return StatusOf(spec.Name, workload) with { Detail = "stop the workload before updating it" };
            }

            workload.Spec = spec;
            workload.ConfigHash = ProcessAdoption.HashOf(spec);
            workload.Updating = true;
            workload.Detail = "updating";
            started = StatusOf(spec.Name, workload);
        }

        Announce(started);
        _ = RunUpdateAsync(source, spec, workload, installRoot);
        return started;
    }

    private async Task RunUpdateAsync(ISourceProvider source, AgentWorkloadSpec spec, SupervisedWorkload workload, string installRoot)
    {
        try
        {
            Directory.CreateDirectory(installRoot);
            var version = await source.AcquireAsync(spec, installRoot, line => Capture(workload, line, LogStream.Stdout), CancellationToken.None);

            SourceMarker.Write(installRoot, version);

            lock (workload.Gate)
            {
                workload.InstalledVersion = version;
                workload.Detail = null;
            }

            logger.LogInformation("Updated '{Workload}' to {Version}", spec.Name, version);
        }
        catch (Exception ex)
        {
            lock (workload.Gate)
            {
                workload.Detail = $"update failed: {ex.Message}";
            }

            Capture(workload, $"Update failed: {ex.Message}", LogStream.Stderr);
            logger.LogError(ex, "Update failed for '{Workload}'", spec.Name);
        }
        finally
        {
            AgentWorkloadStatus settled;
            lock (workload.Gate)
            {
                workload.Updating = false;
                settled = StatusOf(spec.Name, workload);
            }

            Announce(settled);
        }
    }

    public AgentWorkloadStatus GetStatus(string name)
    {
        if (!_workloads.TryGetValue(name, out var workload))
        {
            return new AgentWorkloadStatus { Name = name, Running = false };
        }

        lock (workload.Gate)
        {
            return StatusOf(name, workload);
        }
    }

    private void Start(string name, SupervisedWorkload workload)
    {
        var spec = workload.Spec;

        try
        {
            var (fileName, workingDirectory) = Locate(name, spec);

            if (spec.ManagedDirectory && workingDirectory is not null)
            {
                workload.InstalledVersion = SourceMarker.Read(workingDirectory);
            }

            var info = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                // must be drained or a full pipe blocks the child
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
            };

            foreach (var arg in spec.Args)
            {
                info.ArgumentList.Add(arg);
            }

            foreach (var env in spec.Env)
            {
                info.Environment[env.Key] = env.Value;
            }

            var process = Process.Start(info) ?? throw new InvalidOperationException("Process.Start returned null.");

            workload.Process = process;
            workload.Input = process.StandardInput;
            // os start time, not now. Restore compares against it
            workload.StartedAt = StartTimeOf(process);
            workload.StopRequested = false;
            workload.Adopted = false;
            workload.Detail = null;

            process.OutputDataReceived += (_, e) => Capture(workload, e.Data, LogStream.Stdout);
            process.ErrorDataReceived += (_, e) => Capture(workload, e.Data, LogStream.Stderr);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Watch(name, workload, process);
            logger.LogInformation("Started '{Workload}' as PID {Pid}", name, process.Id);
        }
        catch (Exception ex)
        {
            workload.Process = null;
            workload.Input = null;
            workload.Detail = ex.Message;
            logger.LogError(ex, "Failed to start '{Workload}'", name);
        }
    }

    private (string FileName, string? WorkingDirectory) Locate(string name, AgentWorkloadSpec spec)
    {
        if (!spec.ManagedDirectory)
        {
            return (spec.Command, string.IsNullOrWhiteSpace(spec.WorkingDirectory) ? null : spec.WorkingDirectory);
        }

        if (!InstallPaths.TryRootFor(_root, name, out var installRoot))
        {
            throw new InvalidOperationException($"'{name}' can't be used as a directory name.");
        }

        Directory.CreateDirectory(installRoot);

        if (!InstallPaths.TryResolveCommand(installRoot, spec.Command, out var fileName))
        {
            throw new InvalidOperationException($"'{spec.Command}' resolves outside the workload's own directory.");
        }

        return (fileName, installRoot);
    }

    private void Capture(SupervisedWorkload workload, string? text, LogStream stream)
    {
        if (text is null)
        {
            return; // stream closed
        }

        var line = new WorkloadLogLine { Text = text, Stream = stream, Timestamp = DateTimeOffset.UtcNow };
        workload.Logs.Add(line);
        LogLine?.Invoke(workload.Spec.Name, line);
    }

    public IReadOnlyList<WorkloadLogLine> GetLogs(string name, int tail) =>
        _workloads.TryGetValue(name, out var workload) ? workload.Logs.Tail(tail) : [];

    private void Watch(string name, SupervisedWorkload workload, Process process)
    {
        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => OnExited(name, workload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not watch '{Workload}' for exit — it won't be auto-restarted", name);
        }
    }

    private Process? FindLiveProcess(TrackedWorkload tracked)
    {
        try
        {
            var process = Process.GetProcessById(tracked.Pid);
            if (process.HasExited)
            {
                return null;
            }

            return ProcessAdoption.IsSameProcess(tracked.StartedAtUtc, new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero))
                ? process
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// no gates on purpose, a torn read is just a stale pid and the start-time check rejects it
    private void Persist()
    {
        var state = new AgentState
        {
            Workloads = _workloads
                .Select(kv => (kv.Key, kv.Value.Spec, kv.Value.ConfigHash, kv.Value.Process, kv.Value.StartedAt))
                .Where(w => w.Process is not null && IsAlive(w.Process))
                .Select(w => new TrackedWorkload
                {
                    Name = w.Key,
                    Pid = w.Process!.Id,
                    StartedAtUtc = w.StartedAt,
                    Spec = w.Spec with { Env = [] },
                    ConfigHash = w.ConfigHash,
                })
                .ToList(),
        };

        state.Save(_statePath);
    }

    private void OnExited(string name, SupervisedWorkload workload)
    {
        bool restarting;
        TimeSpan delay;
        AgentWorkloadStatus settled;

        lock (workload.Gate)
        {
            var process = workload.Process;
            if (process is null)
            {
                return; // Stop already claimed it
            }

            workload.LastExitCode = ExitCodeOf(process);
            workload.Process = null;
            workload.Input = null;
            process.Dispose();

            // stayed up long enough, so reset the crash count and backoff
            if (RestartDecision.Recovered(DateTimeOffset.UtcNow - workload.StartedAt))
            {
                workload.RestartCount = 0;
            }

            var verdict = RestartDecision.Decide(
                workload.Spec.RestartPolicy, workload.LastExitCode.Value, workload.StopRequested, workload.RestartCount);

            restarting = verdict.Restart;
            delay = verdict.Delay;

            if (!restarting)
            {
                workload.Detail = verdict.Reason;
                logger.LogInformation("'{Workload}' exited with {ExitCode}; not restarting ({Reason})",
                    name, workload.LastExitCode, verdict.Reason);
                Persist();
                settled = StatusOf(name, workload);
            }
            else
            {
                workload.RestartCount++;
                workload.Detail = $"exited with {workload.LastExitCode}; restarting in {delay.TotalSeconds:0}s";
                logger.LogWarning("'{Workload}' exited with {ExitCode}; restart {Attempt} in {Delay}s",
                    name, workload.LastExitCode, workload.RestartCount, delay.TotalSeconds);
                settled = StatusOf(name, workload);
            }
        }

        Announce(settled);
        if (!restarting)
        {
            return;
        }

        // off the event thread, inline would recurse through Exited on a crash loop
        _ = Task.Run(async () =>
        {
            await Task.Delay(delay);

            AgentWorkloadStatus restarted;
            lock (workload.Gate)
            {
                if (workload.StopRequested || !_workloads.TryGetValue(name, out var current) || !ReferenceEquals(current, workload))
                {
                    return; // stopped or redeployed while we waited
                }

                Start(name, workload);
                Persist();
                restarted = StatusOf(name, workload);
            }

            Announce(restarted);
        });
    }

    /// ask nicely before killing, a SIGKILL'd game server loses unsaved data
    /// each polite step gets the full grace, so worst case is 2x grace before the kill
    private static void Terminate(Process process, StreamWriter? input, AgentWorkloadSpec spec)
    {
        var grace = Math.Max(0, spec.StopGraceSeconds) * 1000;

        try
        {
            foreach (var step in StopLadder.For(spec.StopCommand, input is not null))
            {
                if (process.HasExited)
                {
                    return;
                }

                switch (step)
                {
                    case StopStep.ConsoleCommand:
                        if (WriteStopCommand(input!, spec.StopCommand!) && process.WaitForExit(grace))
                        {
                            return;
                        }

                        break;

                    case StopStep.Signal:
                        if (ProcessSignal.RequestTerminate(process) && process.WaitForExit(grace))
                        {
                            return;
                        }

                        break;

                    default:
                        process.Kill(entireProcessTree: true);
                        return;
                }
            }
        }
        catch (Exception)
        {
            // already gone
        }
    }

    private static bool WriteStopCommand(StreamWriter input, string command)
    {
        try
        {
            input.WriteLine(command);
            input.Flush();
            return true;
        }
        catch (Exception)
        {
            return false; // pipe closed, fall through to the next step
        }
    }

    private static AgentWorkloadStatus StatusOf(string name, SupervisedWorkload workload)
    {
        var process = workload.Process;
        var running = process is not null && IsAlive(process);

        return new AgentWorkloadStatus
        {
            Name = name,
            Running = running,
            MemoryBytes = running ? MemoryOf(process!) : null,
            CpuPercent = running ? CpuPercentOf(workload, process!) : null,
            Reachable = running ? workload.Reachable : null,
            Updating = workload.Updating,
            InstalledVersion = workload.InstalledVersion,
            Pid = running ? process!.Id : null,
            StartedAt = running ? workload.StartedAt : null,
            RestartCount = workload.RestartCount,
            ExitCode = workload.LastExitCode,
            Adopted = running && workload.Adopted,
            Detail = workload.Detail,
        };
    }

    /// % of one core since the last call. first call just sets the baseline and returns null
    private static double? CpuPercentOf(SupervisedWorkload workload, Process process)
    {
        try
        {
            var now = DateTimeOffset.UtcNow;
            var cpu = process.TotalProcessorTime;

            double? percent = null;
            if (workload.LastCpu is { } lastCpu && workload.LastCpuAt is { } lastAt)
            {
                var elapsed = (now - lastAt).TotalMilliseconds;
                if (elapsed > 0)
                {
                    percent = Math.Round((cpu - lastCpu).TotalMilliseconds / elapsed * 100, 1);
                }
            }

            workload.LastCpu = cpu;
            workload.LastCpuAt = now;
            return percent;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static long? MemoryOf(Process process)
    {
        try
        {
            process.Refresh();
            return process.WorkingSet64;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// one short connect per declared port, announces only what changed
    public async Task ProbeAsync(CancellationToken ct)
    {
        foreach (var (name, workload) in _workloads.ToArray())
        {
            if (workload.Process is not { } process || !IsAlive(process))
            {
                continue;
            }

            var reachable = await PortProbe.ReachableAsync(workload.Spec.Ports, ct);

            AgentWorkloadStatus? changed = null;
            lock (workload.Gate)
            {
                if (workload.Reachable != reachable)
                {
                    workload.Reachable = reachable;
                    changed = StatusOf(name, workload);
                }
            }

            if (changed is not null)
            {
                Announce(changed);
            }
        }
    }

    private static DateTimeOffset StartTimeOf(Process process)
    {
        try
        {
            return new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (Exception)
        {
            return DateTimeOffset.UtcNow;
        }
    }

    private static int ExitCodeOf(Process process)
    {
        try
        {
            return process.ExitCode;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static bool IsAlive(Process process)
    {
        try
        {
            return !process.HasExited;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
