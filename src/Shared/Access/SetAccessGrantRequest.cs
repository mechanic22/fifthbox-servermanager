using System.ComponentModel.DataAnnotations;

namespace FifthBox.ServerManager.Shared.Access;

/// idempotent, setting again updates the grant and None removes it
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
