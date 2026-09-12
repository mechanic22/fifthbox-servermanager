using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.ServerManager.Shared.Registries;

namespace FifthBox.ServerManager.App.Registries;

public interface IRegistryService
{
    Task<IReadOnlyList<RegistryResponse>> ListAsync(CancellationToken ct = default);
    Task<RegistryResponse> CreateAsync(CreateRegistryRequest request, CancellationToken ct = default);
    Task<RegistryResponse> UpdateAsync(string id, UpdateRegistryRequest request, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
}

/// Manages private-registry credentials (CRUD) and resolves image → auth for pulls. Passwords are stored
/// encrypted via ISecretProtector and never returned; decryption happens only in ResolveAsync.
public sealed class RegistryService(
    IRegistryRepository registries,
    ISecretProtector protector,
    TimeProvider clock) : IRegistryService, IRegistryAuthResolver
{
    public async Task<IReadOnlyList<RegistryResponse>> ListAsync(CancellationToken ct = default)
        => (await registries.ListAsync(ct)).Select(Map).ToList();

    public async Task<RegistryResponse> CreateAsync(CreateRegistryRequest request, CancellationToken ct = default)
    {
        var domain = Require(request.Domain, nameof(CreateRegistryRequest.Domain), "A registry domain is required.");
        var username = Require(request.Username, nameof(CreateRegistryRequest.Username), "A username is required.");
        if (string.IsNullOrEmpty(request.Password))
        {
            throw new ValidationException(nameof(CreateRegistryRequest.Password), "A password is required.");
        }

        var now = clock.GetUtcNow();
        var registry = new Registry
        {
            Domain = domain,
            Username = username,
            PasswordEnc = protector.Protect(request.Password),
            Prefix = Blank(request.Prefix),
            CreatedAt = now,
            UpdatedAt = now,
        };

        await registries.AddAsync(registry, ct);
        return Map(registry);
    }

    public async Task<RegistryResponse> UpdateAsync(string id, UpdateRegistryRequest request, CancellationToken ct = default)
    {
        var registry = await registries.FindByIdAsync(id, ct) ?? throw new NotFoundException($"Registry '{id}' not found.");
        registry.Domain = Require(request.Domain, nameof(UpdateRegistryRequest.Domain), "A registry domain is required.");
        registry.Username = Require(request.Username, nameof(UpdateRegistryRequest.Username), "A username is required.");
        registry.Prefix = Blank(request.Prefix);

        // Blank password on update = keep the existing one.
        if (!string.IsNullOrEmpty(request.Password))
        {
            registry.PasswordEnc = protector.Protect(request.Password);
        }

        registry.UpdatedAt = clock.GetUtcNow();
        await registries.UpdateAsync(registry, ct);
        return Map(registry);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
        => await registries.RemoveAsync(await registries.FindByIdAsync(id, ct) ?? throw new NotFoundException($"Registry '{id}' not found."), ct);

    public async Task<RegistryAuth?> ResolveAsync(string image, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(image))
        {
            return null;
        }

        var match = (await registries.ListAsync(ct)).FirstOrDefault(r => Matches(r, image));
        return match is null ? null : new RegistryAuth(match.Username, protector.Unprotect(match.PasswordEnc), match.Domain);
    }

    private static bool Matches(Registry registry, string image) =>
        image.StartsWith(registry.Domain + "/", StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrEmpty(registry.Prefix) && image.StartsWith(registry.Prefix, StringComparison.OrdinalIgnoreCase));

    private static string Require(string? value, string member, string message)
    {
        var trimmed = (value ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            throw new ValidationException(member, message);
        }

        return trimmed;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RegistryResponse Map(Registry r) => new()
    {
        Id = r.Id,
        Domain = r.Domain,
        Username = r.Username,
        Prefix = r.Prefix,
    };
}
