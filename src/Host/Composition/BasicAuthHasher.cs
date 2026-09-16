using FifthBox.ServerManager.App.Routes;

namespace FifthBox.ServerManager.Host.Composition;

/// nginx wants $2y$, BCrypt.Net writes $2a$/$2b$, so the prefix gets swapped
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
