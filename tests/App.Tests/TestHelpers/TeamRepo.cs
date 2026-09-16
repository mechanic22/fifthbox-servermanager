using FifthBox.ServerManager.App.Access;
using Moq;

namespace FifthBox.ServerManager.App.Tests;

/// most access tests are about grants, not rosters, so they just use None()
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
