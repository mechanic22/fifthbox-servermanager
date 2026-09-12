namespace FifthBox.ServerManager.Storage.Models;

/// The single active agent enrollment key (its hash only). One row, replaced on rotation.
public class EnrollmentKeyRecord
{
    public string Id { get; set; } = "current";
    public string Hash { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
