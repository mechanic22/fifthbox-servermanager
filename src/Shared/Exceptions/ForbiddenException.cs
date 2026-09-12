namespace FifthBox.ServerManager.Shared.Exceptions;

/// <summary>Caller is logged in but not allowed to do this. Host turns it into a 403.</summary>
public class ForbiddenException(string message) : Exception(message)
{
}
