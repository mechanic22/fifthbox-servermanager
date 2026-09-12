namespace FifthBox.ServerManager.Shared.Access;

/// Who a grant is for. A team grant reaches every member; someone holding both keeps the higher level.
/// Append only — these serialize as numbers, so the order is a wire contract.
public enum AccessSubject
{
    User = 0,
    Team = 1,
}
