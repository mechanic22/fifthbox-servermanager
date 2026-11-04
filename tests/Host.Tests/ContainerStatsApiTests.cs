using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FifthBox.Encryption.Aes;
using FifthBox.ServerManager.App.ContainerStats;
using FifthBox.ServerManager.Shared.Workloads;
using Microsoft.Extensions.DependencyInjection;

namespace FifthBox.ServerManager.Host.Tests;

[TestClass]
public class ContainerStatsApiTests
{
    private static AppFactory _factory = null!;

    [ClassInitialize]
    public static void Init(TestContext _) => _factory = new AppFactory();

    [ClassCleanup]
    public static void Cleanup() => _factory.Dispose();

    [TestMethod]
    public async Task An_unsigned_report_gets_401()
    {
        var response = await _factory.CreateClient().PostAsync("/api/internal/container-stats", new StringContent("{}"));

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task Nothing_gets_in_on_a_generated_encryption_key()
    {
        var body = "{}"u8.ToArray();

        var response = await Post(_factory, ContainerStatsSignature.DeriveKey("anything"), body);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [TestMethod]
    public async Task A_signed_report_goes_through_the_real_pipeline()
    {
        var encryptionKey = AesGenerator.GenerateKey();
        using var factory = new AppFactory(encryptionKey);
        var body = JsonSerializer.SerializeToUtf8Bytes(new ContainerStatsReport
        {
            Containers = [new ContainerStatsEntry { TaskId = "task1", CpuPercent = 5, MemoryBytes = 10 }],
            Machine = new MachineStats { NodeId = "node1", CpuPercent = 7, MemoryUsedBytes = 1, MemoryTotalBytes = 2 },
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        var response = await Post(factory, ContainerStatsSignature.DeriveKey(encryptionKey), body);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual(5, factory.Services.GetRequiredService<IContainerStatsStore>().Find("task1")!.CpuPercent);

        var admin = await factory.AdminClientAsync();
        var machines = await admin.GetFromJsonAsync<List<MachineStatsHistory>>("/api/nodes/machine-stats/history");
        Assert.AreEqual("node1", machines!.Single().NodeId);
    }

    [TestMethod]
    [DataRow("/api/nodes/machine-stats/history")]
    public async Task Machine_stats_are_closed_to_anonymous_callers(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> Post(AppFactory factory, string key, byte[] body)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/internal/container-stats") { Content = new ByteArrayContent(body) };
        request.Headers.Add(ContainerStatsHeaders.Timestamp, now.ToString());
        request.Headers.Add(ContainerStatsHeaders.Signature, ContainerStatsSignature.Sign(key, now, body));
        return await factory.CreateClient().SendAsync(request);
    }

    [TestMethod]
    public async Task An_oversized_report_is_refused_before_anything_reads_it()
    {
        var body = new StringContent(new string('x', ContainerStatsIngest.MaxBodyBytes + 1));

        var response = await _factory.CreateClient().PostAsync("/api/internal/container-stats", body);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
