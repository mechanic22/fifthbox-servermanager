namespace FifthBox.ServerManager.Storage.Models;

/// one row, hash only, replaced on rotation
public class EnrollmentKeyRecord
{
    public string Id { get; set; } = "current";
    public string Hash { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}
