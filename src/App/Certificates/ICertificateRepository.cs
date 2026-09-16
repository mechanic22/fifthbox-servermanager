namespace FifthBox.ServerManager.App.Certificates;

public interface ICertificateRepository
{
    Task<IReadOnlyList<Certificate>> ListAsync(CancellationToken ct = default);
    Task<Certificate?> FindByHostnameAsync(string hostname, CancellationToken ct = default);
    Task AddAsync(Certificate certificate, CancellationToken ct = default);
    Task UpdateAsync(Certificate certificate, CancellationToken ct = default);
    Task RemoveAsync(Certificate certificate, CancellationToken ct = default);
}
