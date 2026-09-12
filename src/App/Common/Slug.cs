using System.Text;

namespace FifthBox.ServerManager.App.Common;

/// URL/DNS-friendly slug generation. Lowercases, keeps letters/digits, collapses everything else to
/// single dashes. Used for swarm service names (which must be DNS-label-ish).
public static class Slug
{
    public static string Make(string value)
    {
        var sb = new StringBuilder(value.Length);
        var lastDash = false;
        foreach (var ch in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastDash = false;
            }
            else if (!lastDash && sb.Length > 0)
            {
                sb.Append('-');
                lastDash = true;
            }
        }
        return sb.ToString().Trim('-');
    }
}
