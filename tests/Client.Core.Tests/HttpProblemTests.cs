using System.Net;
using System.Net.Http.Json;
using FifthBox.ServerManager.Client.Core;

namespace FifthBox.ServerManager.Client.Core.Tests;

[TestClass]
public class HttpProblemTests
{
    private static HttpResponseMessage Response(HttpStatusCode status, object? problem = null)
    {
        var response = new HttpResponseMessage(status);
        if (problem is not null)
        {
            response.Content = JsonContent.Create(problem);
        }

        return response;
    }

    [TestMethod]
    public async Task Carries_the_status_so_a_page_can_tell_missing_from_broken()
    {
        using var missing = Response(HttpStatusCode.NotFound, new { detail = "No such workload." });
        var notFound = await HttpProblem.ToExceptionAsync(missing, TestContext.CancellationTokenSource.Token);

        Assert.IsTrue(notFound.IsNotFound);
        Assert.AreEqual("No such workload.", notFound.Message);

        using var broken = Response(HttpStatusCode.ServiceUnavailable);
        var failed = await HttpProblem.ToExceptionAsync(broken, TestContext.CancellationTokenSource.Token);

        Assert.IsFalse(failed.IsNotFound);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
    }

    [TestMethod]
    public async Task Validation_errors_beat_detail_and_title()
    {
        using var response = Response(HttpStatusCode.BadRequest, new
        {
            title = "One or more validation errors occurred.",
            detail = "See errors.",
            errors = new Dictionary<string, string[]> { ["Image"] = ["Image is required."] },
        });

        var ex = await HttpProblem.ToExceptionAsync(response, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual("Image is required.", ex.Message);
    }

    [TestMethod]
    public async Task A_body_that_isnt_problem_details_still_gives_a_usable_message()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("<html>nope</html>"),
        };

        var ex = await HttpProblem.ToExceptionAsync(response, TestContext.CancellationTokenSource.Token);

        Assert.AreEqual("You don't have permission to do that.", ex.Message);
        Assert.AreEqual(HttpStatusCode.Forbidden, ex.StatusCode);
    }

    public TestContext TestContext { get; set; } = default!;
}
