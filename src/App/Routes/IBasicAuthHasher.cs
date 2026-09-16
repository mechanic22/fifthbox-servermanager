namespace FifthBox.ServerManager.App.Routes;

/// has to be bcrypt ($2y$) or similar that nginx crypt understands, never a fast hash
public interface IBasicAuthHasher
{
    string Hash(string password);
}
