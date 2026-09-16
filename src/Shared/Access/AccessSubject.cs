namespace FifthBox.ServerManager.Shared.Access;

/// team grants reach every member, highest level wins
/// append only, these go over the wire as numbers
public enum AccessSubject
{
    User = 0,
    Team = 1,
}
