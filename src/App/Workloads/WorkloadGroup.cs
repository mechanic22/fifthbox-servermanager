namespace FifthBox.ServerManager.App.Workloads;

/// A named grouping of workloads (like the archive's application groups). Groups nest via ParentId
/// (null = top level). Workloads reference a group by id; deleting a group reparents its subgroups up to
/// its own parent and ungroups its direct workloads rather than removing anything.
public class WorkloadGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string Name { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
