namespace FifthBox.ServerManager.App.Platform;

public sealed class EncryptionOptions
{
    /// key was invented since none was configured, anything encrypted under it is gone after a restart
    public bool Ephemeral { get; set; }
}
