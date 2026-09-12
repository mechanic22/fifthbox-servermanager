namespace FifthBox.ServerManager.Shared.Exceptions;

/// <summary>Thing doesn't exist. Host turns it into a 404.</summary>
public class NotFoundException(string message) : Exception(message)
{
}
