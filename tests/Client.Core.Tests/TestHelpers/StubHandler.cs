namespace FifthBox.ServerManager.Client.Core.Tests;

/// <summary>
/// A terminal <see cref="HttpMessageHandler"/> that answers each request via a caller-supplied
/// responder and records what it saw (method, URI, and the Authorization parameter as attached by the
/// handler under test).
/// </summary>
internal sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<(string Method, string Uri, string? Auth)> Seen { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Seen.Add((request.Method.Method, request.RequestUri!.AbsoluteUri, request.Headers.Authorization?.Parameter));
        return Task.FromResult(responder(request));
    }
}
