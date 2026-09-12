using FifthBox.ServerManager.App.Access;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

/// <summary>
/// <see cref="ITeamRepository"/> doubles. Most access tests are about grants rather than rosters, so
/// they take <see cref="None"/> and the team path resolves to nothing.
/// </summary>
internal static class TeamRepo
{
    public static ITeamRepository None() => Of();

    public static ITeamRepository Of(params Team[] teams)
    {
        var stored = teams.ToList();
        var mock = new Mock<ITeamRepository>();
        mock.Setup(t => t.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        mock.Setup(t => t.ListForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) =>
                (IReadOnlyList<Team>)[.. stored.Where(t => t.MemberIds.Contains(userId, StringComparer.Ordinal))]);
        mock.Setup(t => t.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => stored.FirstOrDefault(t => t.Id == id));
        return mock.Object;
    }
}
