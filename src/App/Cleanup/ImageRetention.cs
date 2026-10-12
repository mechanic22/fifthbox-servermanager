namespace FifthBox.ServerManager.App.Cleanup;

public static class ImageRetention
{
    /// everything except what a container uses, plus the newest other image of each repository in use,
    /// so a rollback to the previous tag doesn't need the registry
    public static IReadOnlyList<LocalImage> SelectForRemoval(IReadOnlyList<LocalImage> images, IReadOnlySet<string> inUse)
    {
        var used = images.Where(i => inUse.Contains(i.Id)).ToList();
        var keep = used.Select(i => i.Id).ToHashSet();

        foreach (var repository in used.SelectMany(Repositories).Distinct())
        {
            var previous = images
                .Where(i => !keep.Contains(i.Id) && Repositories(i).Contains(repository))
                .MaxBy(i => i.Created);
            if (previous is not null)
                keep.Add(previous.Id);
        }

        return [.. images.Where(i => !keep.Contains(i.Id))];
    }

    /// what people would call it, "repo:tag", falling back to the digest or the short id
    public static string NameOf(LocalImage image) =>
        image.Tags.FirstOrDefault(t => t != "<none>:<none>")
        ?? image.Digests.FirstOrDefault(d => d != "<none>@<none>")
        ?? ShortId(image.Id);

    // "docker.5thbox.com:5000/fbsm/app:1.2" -> "docker.5thbox.com:5000/fbsm/app", the tag colon is the one after the last slash
    internal static IEnumerable<string> Repositories(LocalImage image) =>
        image.Tags.Where(t => t != "<none>:<none>").Select(t => t[..TagColon(t)])
            .Concat(image.Digests.Where(d => d.Contains('@') && d != "<none>@<none>").Select(d => d[..d.IndexOf('@')]))
            .Distinct();

    private static int TagColon(string tag)
    {
        var colon = tag.LastIndexOf(':');
        return colon > tag.LastIndexOf('/') ? colon : tag.Length;
    }

    private static string ShortId(string id) => id.StartsWith("sha256:") ? id[7..19] : id[..Math.Min(12, id.Length)];
}
