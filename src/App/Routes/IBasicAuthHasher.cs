namespace FifthBox.ServerManager.App.Routes;

/// Hashes a route's basic-auth password into an htpasswd-compatible entry. Implemented in the Host
/// composition root (the algorithm/library choice lives there); the App stays dependency-free. Must be a
/// vetted adaptive hash nginx's crypt understands — bcrypt ($2y$…), never a fast/plain hash.
public interface IBasicAuthHasher
{
    string Hash(string password);
}
