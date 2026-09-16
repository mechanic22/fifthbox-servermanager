namespace FifthBox.ServerManager.Client.Web.Components;

public sealed class KeyValueItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Secret { get; set; }
}
