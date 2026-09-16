namespace FifthBox.ServerManager.Shared.Workloads;

public record PortMapping(int Published, int Target, PortProtocol Protocol, PortPublishMode Mode = PortPublishMode.Ingress);
