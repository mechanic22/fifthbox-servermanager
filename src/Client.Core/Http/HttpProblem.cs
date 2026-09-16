using System.Net;
using System.Net.Http.Json;

namespace FifthBox.ServerManager.Client.Core;

public static class HttpProblem
{
    /// keeps the status so callers can tell not-found from broken
    public static async Task<ApiException> ToExceptionAsync(HttpResponseMessage response, CancellationToken ct)
        => new(await ReadMessageAsync(response, ct), response.StatusCode);

    public static async Task<string> ReadMessageAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemInfo>(ct);
            if (problem?.Errors is { Count: > 0 } errors)
            {
                return string.Join(" ", errors.Values.SelectMany(v => v));
            }

            if (!string.IsNullOrWhiteSpace(problem?.Detail))
            {
                return problem!.Detail!;
            }

            if (!string.IsNullOrWhiteSpace(problem?.Title))
            {
                return problem!.Title!;
            }
        }
        catch
        {
            // not ProblemDetails, fall through to the generic message
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Incorrect email or password.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            _ => "Something went wrong. Please try again."
        };
    }

    private sealed class ProblemInfo
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}

/// message is safe to show the user
public sealed class ApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;

    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
}
