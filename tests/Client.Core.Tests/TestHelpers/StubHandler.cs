namespace FifthBox.ServerManager.Client.Core.Tests;

internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<(string Method, string Uri, string? Auth)> Seen { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Seen.Add((request.Method.Method, request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.Parameter));
        return Task.FromResult(responder(request));
    }
}
