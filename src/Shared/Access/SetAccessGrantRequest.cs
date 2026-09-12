using System.ComponentModel.DataAnnotations;

namespace FifthBox.ServerManager.Shared.Access;

/// Grants one user or team one level on one group or workload. Idempotent: setting a level a second
/// time updates the existing grant, and <see cref="AccessLevel.None"/> removes it.
public class SetAccessGrantRequest
{
    public AccessSubject SubjectType { get; set; }

    [Required]
    public string SubjectId { get; set; } = string.Empty;

    public AccessScope Scope { get; set; }

    [Required]
    public string TargetId { get; set; } = string.Empty;

    public AccessLevel Level { get; set; }
}
