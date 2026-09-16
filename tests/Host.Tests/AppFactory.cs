using System.Net.Http.Json;
using FifthBox.ServerManager.Shared.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FifthBox.ServerManager.Host.Tests;

public sealed class AppFactory : WebApplicationFactory<Program>
{
    public const string AdminUserName = "admin@test.local";
    public const string AdminPassword = "test-admin-password";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fbsm-test-{Guid.NewGuid():n}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.UseSetting("ConnectionStrings:AppDb", $"Data Source={_dbPath}");
        builder.UseSetting("Identity:DemoSeedAdmin:UserName", AdminUserName);
        builder.UseSetting("Identity:DemoSeedAdmin:Password", AdminPassword);
        // cookie is Secure by default and the test client talks http
        builder.UseSetting("Identity:AllowInsecureCookies", "true");

        builder.ConfigureServices(services =>
        {
            // drops the docker event listener and job runner, no test has a daemon
            foreach (var hosted in services.Where(d => d.ServiceType == typeof(IHostedService)).ToList())
            {
                services.Remove(hosted);
            }
        });
    }

    /// already signed in as the seeded admin
    public async Task<HttpClient> AdminClientAsync()
        => await SignInAsync(AdminUserName, AdminPassword);

    public async Task<HttpClient> SignInAsync(string userName, string password)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest { UserName = userName, Password = password });

        Assert.IsTrue(response.IsSuccessStatusCode, $"Sign-in as {userName} failed: {response.StatusCode}");
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            // sqlite pools connections per file, without this the delete loses to a live handle
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            TryDelete(_dbPath);
            TryDelete(_dbPath + "-wal");
            TryDelete(_dbPath + "-shm");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // leftover temp file isn't worth failing a run over
        }
    }
}
