using Docker.DotNet.Models;
using FifthBox.ServerManager.App.Workloads;
using FifthBox.ServerManager.Shared.Workloads;

namespace FifthBox.ServerManager.Integrations.Swarm;

/// Folds a full service list and a full task list into one status per service. The tested core of the
/// bulk sweep; the adapter around it only makes the two calls.
public static class SwarmStatusMapper
{
    public static IReadOnlyDictionary<string, WorkloadRuntimeStatus> Derive(
        IEnumerable<SwarmService> services,
        IEnumerable<TaskResponse> tasks)
    {
        // Docker keeps shut-down tasks around as history — a service with one replica that has restarted
        // twice has three tasks, and only one of them counts toward "running". The rest are what explain
        // a service that never got there.
        var byService = tasks
            .Where(t => t.ServiceID is not null)
            .GroupBy(t => t.ServiceID, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var statuses = new Dictionary<string, WorkloadRuntimeStatus>(StringComparer.Ordinal);

        foreach (var service in services)
        {
            if (service.Spec?.Name is not { Length: > 0 } name)
            {
                continue;
            }

            List<TaskResponse> serviceTasks = service.ID is null ? [] : byService.GetValueOrDefault(service.ID, []);
            var taskHistory = SwarmTaskMapper.ToTasks(serviceTasks);

            statuses[name] = WorkloadSpecMapper.ToRuntimeStatus(
                name,
                deployed: true,
                desired: WorkloadSpecMapper.DesiredCount(service.Spec, serviceTasks),
                running: serviceTasks.Count(t => t.Status?.State == TaskState.Running)) with
            {
                UpdateState = service.UpdateStatus?.State,
                UpdateMessage = service.UpdateStatus?.Message,
                RolloutStalled = Rollout.Stalled(service.UpdateStatus?.State),
                Tasks = taskHistory,
                LastError = SwarmTaskMapper.LastError(taskHistory),
                RunningImage = service.Spec.TaskTemplate?.ContainerSpec?.Image,
            };
        }

        return statuses;
    }
}
