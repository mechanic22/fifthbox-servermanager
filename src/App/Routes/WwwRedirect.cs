namespace FifthBox.ServerManager.App.Routes;

/// row existing means www.{Hostname} 301s to the bare hostname
public class WwwRedirect
{
    public string Hostname { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
