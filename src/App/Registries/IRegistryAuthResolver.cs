namespace FifthBox.ServerManager.App.Registries;

/// Credentials for pulling one image from a private registry.
public sealed record RegistryAuth(string Username, string Password, string ServerAddress);

/// Resolves an image name to its registry's pull credentials (decrypted just-in-time), or null when no
/// configured registry matches. Consumed by the Swarm backend on deploy — keeps it free of a Storage
/// dependency.
public interface IRegistryAuthResolver
{
    Task<RegistryAuth?> ResolveAsync(string image, CancellationToken ct = default);
}
