using System.ComponentModel.DataAnnotations;

namespace FifthBox.ServerManager.Shared.Teams;

/// Creates a team, or updates an existing one's name and description. Membership is set separately.
public class SaveTeamRequest
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string Description { get; set; } = string.Empty;
}
