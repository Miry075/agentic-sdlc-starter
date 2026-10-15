using Knowledge.Core;
using Knowledge.Policy;
using Microsoft.Extensions.Logging;

namespace Knowledge.Upstreams;

/// <summary>
/// All upstream connections. Offers their approved tools (and resources) to the dispatcher, and tells the
/// policy whether an upstream tool still matches the definition an administrator approved.
/// </summary>
public sealed class UpstreamPool(IEnumerable<UpstreamConnection> connections, ILoggerFactory loggers) : IToolSource, IToolPinStatus
{
    private readonly IReadOnlyList<UpstreamConnection> _connections = connections.ToList();
    private readonly ILogger _logger = loggers.CreateLogger<UpstreamPool>();

    public IReadOnlyList<UpstreamConnection> Connections => _connections;

    public IReadOnlyCollection<IGatewayTool> GetTools()
    {
        var tools = new List<IGatewayTool>();

        foreach (var connection in _connections.Where(c => c.IsReady))
        {
            tools.AddRange(connection.ApprovedDefinitions.Select(d => new UpstreamTool(connection, d, _logger)));

            if (connection.Policy.ResourceAllow.Count > 0)
            {
                tools.Add(new UpstreamResourceTool(connection));
            }
        }

        return tools;
    }

    public bool IsApproved(string qualifiedToolName)
    {
        var dot = qualifiedToolName.IndexOf('.');
        if (dot <= 0)
        {
            return false;
        }

        var connection = _connections.FirstOrDefault(c => c.Name == qualifiedToolName[..dot]);
        var tool = qualifiedToolName[(dot + 1)..];

        return connection is not null
            && (tool == PolicyLoader.ReadResourceTool ? connection.IsReady : connection.IsToolApproved(tool));
    }

    /// <summary>Connects and discovers every upstream. One upstream failing never stops the others.</summary>
    public async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        foreach (var connection in _connections)
        {
            try
            {
                await connection.RefreshAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not refresh upstream {Upstream}. Its tools stay unavailable.", connection.Name);
            }
        }
    }
}
