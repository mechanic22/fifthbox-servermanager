namespace FifthBox.ServerManager.App.Registries;

public sealed record RegistryAuth(string Username, string Password, string ServerAddress);

/// decrypted just in time, null when no registry matches
public interface IRegistryAuthResolver
{
    Task<RegistryAuth?> ResolveAsync(string image, CancellationToken ct = default);
}
