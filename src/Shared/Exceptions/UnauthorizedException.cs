namespace FifthBox.ServerManager.Shared.Exceptions;

/// keep the message generic so wrong password and unknown user look the same
public class UnauthorizedException(string message) : Exception(message)
{
}
