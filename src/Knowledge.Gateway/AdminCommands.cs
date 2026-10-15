using Knowledge.Upstreams;

namespace Knowledge.Gateway;

/// <summary>
/// <c>upstreams list</c> shows what each upstream offers and whether it matches the policy;
/// <c>upstreams approve NAME</c> prints the pins to paste into the policy. Neither edits any file:
/// pins change through a pull request reviewed by the policy owners.
/// </summary>
public static class AdminCommands
{
    public static bool IsAdminCommand(string[] args) => args.Length >= 1 && args[0] == "upstreams";

    public static async Task<int> RunAsync(string[] args, IServiceProvider services, TextWriter output)
    {
        var pool = services.GetRequiredService<UpstreamPool>();
        await pool.RefreshAllAsync(CancellationToken.None);

        switch (args)
        {
            case ["upstreams", "list"]:
                await output.WriteLineAsync(UpstreamReport.Format(pool.Connections));
                return 0;

            case ["upstreams", "approve", var name]:
                var connection = pool.Connections.FirstOrDefault(c => c.Name == name);
                if (connection is null)
                {
                    await output.WriteLineAsync($"No upstream named '{name}'. Configured: {string.Join(", ", pool.Connections.Select(c => c.Name))}");
                    return 1;
                }

                await output.WriteLineAsync(UpstreamReport.FormatApproval(connection));
                return 0;

            default:
                await output.WriteLineAsync("Usage: upstreams list | upstreams approve <name>");
                return 2;
        }
    }
}
