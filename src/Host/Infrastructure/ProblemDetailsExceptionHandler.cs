using FifthBox.ServerManager.Shared.Exceptions;
using FifthBox.Identity.AspNetCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace FifthBox.ServerManager.Host.Infrastructure;

/// <summary>
/// The one place exceptions turn into HTTP responses. Maps the canonical exceptions to status codes
/// with <see cref="ProblemDetails"/> bodies; anything else becomes a generic 500 that leaks nothing.
/// Expected failures log at Debug, unexpected ones at Error.
/// </summary>
public sealed class ProblemDetailsExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Identity exceptions map via the FifthBox.Identity.AspNetCore helper (the package owns those
        // types); everything else maps to the app's own Shared canonical set. Still one translation point.
        if (!IdentityProblemDetails.TryMap(exception, out var status, out var title))
        {
            (status, title) = exception switch
            {
                ValidationException => (StatusCodes.Status400BadRequest, "One or more validation errors occurred."),
                UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized."),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden."),
                NotFoundException => (StatusCodes.Status404NotFound, "Resource not found."),
                ConflictException => (StatusCodes.Status409Conflict, "Conflict."),
                _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
            };
        }

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception");
        }
        else
        {
            logger.LogDebug(exception, "Handled {ExceptionType}", exception.GetType().Name);
        }

        var problemDetails = new ProblemDetails { Status = status, Title = title };

        // Only surface details for expected/canonical failures; a 500 stays generic.
        if (status != StatusCodes.Status500InternalServerError)
        {
            problemDetails.Detail = exception.Message;
        }

        if (exception is ValidationException validation)
        {
            problemDetails.Extensions["errors"] = validation.Errors;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
