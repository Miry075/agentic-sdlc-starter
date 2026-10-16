using System.Text;

namespace Knowledge.Upstreams;

/// <summary>Text output of the <c>upstreams list</c> and <c>upstreams approve</c> admin commands.</summary>
public static class UpstreamReport
{
    public static string Format(IEnumerable<UpstreamConnection> connections)
    {
        var text = new StringBuilder();

        foreach (var connection in connections)
        {
            text.AppendLine($"{connection.Name} (layer {connection.Layer})");
            if (connection.Discovered.Count == 0)
            {
                text.AppendLine("  no tools discovered (upstream unreachable?)");
            }

            foreach (var tool in connection.Discovered.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                text.AppendLine($"  {tool.Name,-30} {tool.Status,-10} {tool.Pin}");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>The pins to paste into the policy file for tools it already declares. Never edits the file.</summary>
    public static string FormatApproval(UpstreamConnection connection)
    {
        var text = new StringBuilder();
        text.AppendLine($"# Paste into policy/phase-access.yaml under upstreams.{connection.Name}.tools, then review in a pull request.");

        foreach (var tool in connection.Discovered
                     .Where(t => t.Status is PinStatus.Mismatch or PinStatus.NotPinned or PinStatus.Approved)
                     .OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            text.AppendLine($"{tool.Name}: {{ pin: \"{tool.Pin}\" }}");
        }

        var undeclared = connection.Discovered.Where(t => t.Status == PinStatus.Undeclared).Select(t => t.Name).Order().ToList();
        if (undeclared.Count > 0)
        {
            text.AppendLine($"# Offered by the upstream but not declared in the policy (not exposed): {string.Join(", ", undeclared)}");
        }

        return text.ToString().TrimEnd();
    }
}
