using FifthBox.ServerManager.Shared.Exceptions;
using Microsoft.AspNetCore.SignalR;

namespace FifthBox.ServerManager.Host.Hubs;

/// The hub-side counterpart to the HTTP problem-details handler: canonical exceptions carry their
/// message to the client, anything else is logged and reported as a generic failure.
public sealed class CanonicalExceptionHubFilter(ILogger<CanonicalExceptionHubFilter> logger) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext context, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception ex) when (ex is ValidationException or UnauthorizedException or ForbiddenException
            or NotFoundException or ConflictException)
        {
            logger.LogDebug(ex, "{Hub}.{Method} refused", context.Hub.GetType().Name, context.HubMethodName);
            throw new HubException(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Hub}.{Method} failed", context.Hub.GetType().Name, context.HubMethodName);
            throw new HubException("An unexpected error occurred.");
        }
    }
}
