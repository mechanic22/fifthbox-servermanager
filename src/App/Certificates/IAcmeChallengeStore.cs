namespace FifthBox.ServerManager.App.Certificates;

/// The HTTP-01 tokens an in-flight order is waiting on. Implemented in the Host, in memory: a challenge
/// lives for seconds, and losing one to a restart just costs the order a retry.
public interface IAcmeChallengeStore
{
    void Publish(string token, string keyAuthorization);

    /// What to serve for a token, or null when we aren't expecting it.
    string? Resolve(string token);

    void Clear(string token);
}
