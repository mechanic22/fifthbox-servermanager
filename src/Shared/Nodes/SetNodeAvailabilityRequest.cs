namespace FifthBox.ServerManager.Shared.Nodes;

public record SetNodeAvailabilityRequest
{
    public NodeAvailability Availability { get; init; }
}
