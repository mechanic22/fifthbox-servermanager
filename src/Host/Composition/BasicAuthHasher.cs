using FifthBox.ServerManager.App.Routes;

namespace FifthBox.ServerManager.Host.Composition;

/// Bridges the App's IBasicAuthHasher to BCrypt.Net. bcrypt is a vetted adaptive hash; nginx's crypt
/// accepts the `$2y$` variant, so we normalize BCrypt.Net's `$2a$/$2b$` prefix to it for the htpasswd file.
/// A single-class library bridge with no decision logic — an adapter for an intentionally-open interface.
public sealed class BasicAuthHasher : IBasicAuthHasher
{
    public string Hash(string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        return hash.StartsWith("$2a$", StringComparison.Ordinal) || hash.StartsWith("$2b$", StringComparison.Ordinal)
            ? string.Concat("$2y$", hash.AsSpan(4))
            : hash;
    }
}
