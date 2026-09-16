using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Platform;

namespace FifthBox.ServerManager.Client.Core;

public interface ISystemClient
{
    Task<IReadOnlyList<PlatformServiceResponse>> ListServicesAsync(CancellationToken ct = default);
    Task RestartServiceAsync(string name, CancellationToken ct = default);
    Task<HostDeploymentInfo> GetDeploymentAsync(CancellationToken ct = default);
    Task<PlatformSettingsResponse> GetSettingsAsync(CancellationToken ct = default);
    Task<PlatformSettingsResponse> UpdateSettingsAsync(UpdatePlatformSettingsRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<BackupFileResponse>> ListBackupsAsync(CancellationToken ct = default);
    Task<BackupFileResponse> CreateBackupAsync(CancellationToken ct = default);
    Task DeleteBackupAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduledJobStatus>> ListJobsAsync(CancellationToken ct = default);
    Task<ScheduledJobStatus> RunJobAsync(string name, CancellationToken ct = default);
    Task<RotateEncryptionKeyResponse> RotateEncryptionKeyAsync(RotateEncryptionKeyRequest request, CancellationToken ct = default);
}

public sealed class SystemClient(HttpClient http) : ISystemClient
{
    public async Task<IReadOnlyList<PlatformServiceResponse>> ListServicesAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/system/services", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<PlatformServiceResponse>>(ct) ?? [];
    }

    public async Task RestartServiceAsync(string name, CancellationToken ct = default)
    {
        using var response = await http.PostAsync($"api/system/services/{name}/restart", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    public async Task<HostDeploymentInfo> GetDeploymentAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/system/deployment", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<HostDeploymentInfo>(ct))!;
    }

    public async Task<PlatformSettingsResponse> GetSettingsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/system/settings", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<PlatformSettingsResponse>(ct))!;
    }

    public async Task<PlatformSettingsResponse> UpdateSettingsAsync(UpdatePlatformSettingsRequest request, CancellationToken ct = default)
    {
        using var response = await http.PutAsJsonAsync("api/system/settings", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<PlatformSettingsResponse>(ct))!;
    }

    public async Task<IReadOnlyList<BackupFileResponse>> ListBackupsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/system/backups", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<BackupFileResponse>>(ct) ?? [];
    }

    public async Task<BackupFileResponse> CreateBackupAsync(CancellationToken ct = default)
    {
        using var response = await http.PostAsync("api/system/backups", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<BackupFileResponse>(ct))!;
    }

    public async Task DeleteBackupAsync(string name, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"api/system/backups/{name}", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }
    }

    public async Task<IReadOnlyList<ScheduledJobStatus>> ListJobsAsync(CancellationToken ct = default)
    {
        using var response = await http.GetAsync("api/system/jobs", ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return await response.Content.ReadFromJsonAsync<List<ScheduledJobStatus>>(ct) ?? [];
    }

    public async Task<ScheduledJobStatus> RunJobAsync(string name, CancellationToken ct = default)
    {
        using var response = await http.PostAsync($"api/system/jobs/{name}/run", null, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<ScheduledJobStatus>(ct))!;
    }

    public async Task<RotateEncryptionKeyResponse> RotateEncryptionKeyAsync(RotateEncryptionKeyRequest request, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync("api/system/encryption/rotate", request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw await HttpProblem.ToExceptionAsync(response, ct);
        }

        return (await response.Content.ReadFromJsonAsync<RotateEncryptionKeyResponse>(ct))!;
    }
}
