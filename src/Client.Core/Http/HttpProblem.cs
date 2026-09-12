using System.Net;
using System.Net.Http.Json;

namespace FifthBox.ServerManager.Client.Core;

/// <summary>
/// Turns a ProblemDetails body from a failed response into a single user-facing message. Shared by the
/// typed HTTP clients so they all handle backend failures the same way — join validation errors, else
/// detail/title, else a generic fallback based on the status. Public so heads outside this assembly
/// (the web cookie client) can reuse the same translation.
/// </summary>
public static class HttpProblem
{
    /// The message plus the status, so a caller can tell "this doesn't exist" from "this broke".
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
            // Fall through to the generic message below if the body isn't ProblemDetails.
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Incorrect email or password.",
            HttpStatusCode.Forbidden => "You don't have permission to do that.",
            _ => "Something went wrong. Please try again."
        };
    }

    /// <summary>Just enough to read a ProblemDetails body without pulling in MVC.</summary>
    private sealed class ProblemInfo
    {
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}

/// <summary>A failed API call, carrying a message that's safe to show the user.</summary>
public sealed class ApiException(string message, HttpStatusCode? statusCode = null) : Exception(message)
{
    public HttpStatusCode? StatusCode { get; } = statusCode;

    public bool IsNotFound => StatusCode == HttpStatusCode.NotFound;
}
