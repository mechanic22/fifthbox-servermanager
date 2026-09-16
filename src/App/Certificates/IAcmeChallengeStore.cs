namespace FifthBox.ServerManager.App.Certificates;

/// in memory, challenges live for seconds and a restart just costs a retry
public interface IAcmeChallengeStore
{
    void Publish(string token, string keyAuthorization);

    /// null when we aren't expecting that token
    string? Resolve(string token);

    void Clear(string token);
}
