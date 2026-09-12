namespace FifthBox.ServerManager.Shared.Exceptions;

/// <summary>
/// Auth failed (bad password, unknown user, locked out, bad token). Host turns it into a 401.
/// Keep the message generic so wrong-password and unknown-user look identical — no user enumeration.
/// </summary>
public class UnauthorizedException(string message) : Exception(message)
{
}
