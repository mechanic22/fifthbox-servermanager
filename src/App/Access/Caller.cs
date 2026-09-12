namespace FifthBox.ServerManager.App.Access;

/// Who is asking. The Host reads it off the request's claims and passes it in — App never touches
/// the Identity package or ambient request state. `default` is ("", false), a nobody, so a call site
/// that forgets to pass one fails closed.
public readonly record struct Caller(string UserId, bool IsAdmin)
{
    /// Background work with no request behind it.
    public static Caller System { get; } = new("system", true);
}
