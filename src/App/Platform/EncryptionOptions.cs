namespace FifthBox.ServerManager.App.Platform;

public sealed class EncryptionOptions
{
    /// Set by the composition root when it had to invent a key because none was configured. Anything
    /// encrypted under it is unreadable after a restart, so the UI has to say so.
    public bool Ephemeral { get; set; }
}
