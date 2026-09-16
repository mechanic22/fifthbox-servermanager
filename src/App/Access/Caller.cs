namespace FifthBox.ServerManager.App.Access;

/// default is ("", false), a nobody, so forgetting to pass one fails closed
public readonly record struct Caller(string UserId, bool IsAdmin)
{
    public static Caller System { get; } = new("system", true);
}
