namespace FifthBox.ServerManager.Client.Web.Components;

/// A mutable key/value row for <see cref="KeyValueEditor"/>. Records (like EnvVar) are immutable, so the
/// editor works over these and the parent converts to/from the domain type.
public sealed class KeyValueItem
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Secret { get; set; }
}
