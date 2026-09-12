namespace FifthBox.ServerManager.Shared.Exceptions;

/// <summary>Request clashes with current state (e.g. a duplicate). Host turns it into a 409.</summary>
public class ConflictException(string message) : Exception(message)
{
}
