using FifthBox.ServerManager.App.Certificates;
using Microsoft.EntityFrameworkCore;

namespace FifthBox.ServerManager.Storage.Repositories;

public class EfCertificateRepository(AppDbContext db) : ICertificateRepository
{
    // tracked on purpose, renewal edits the listed rows and saves via UpdateAsync
    public async Task<IReadOnlyList<Certificate>> ListAsync(CancellationToken ct = default)
        => await db.Certificates.OrderBy(c => c.Hostname).ToListAsync(ct);

    public Task<Certificate?> FindByHostnameAsync(string hostname, CancellationToken ct = default)
        => db.Certificates.FirstOrDefaultAsync(c => c.Hostname == hostname, ct);

    public async Task AddAsync(Certificate certificate, CancellationToken ct = default)
    {
        db.Certificates.Add(certificate);
        await db.SaveChangesAsync(ct);
    }

    public Task UpdateAsync(Certificate certificate, CancellationToken ct = default)
        => db.SaveChangesAsync(ct);

    public async Task RemoveAsync(Certificate certificate, CancellationToken ct = default)
    {
        db.Certificates.Remove(certificate);
        await db.SaveChangesAsync(ct);
    }
}
