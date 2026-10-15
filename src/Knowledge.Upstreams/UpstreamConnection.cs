using System.Text.Json.Nodes;
using Knowledge.Core;
using Knowledge.Policy;
using Microsoft.Extensions.Logging;

namespace Knowledge.Upstreams;

public enum PinStatus
{
    /// <summary>Declared in the policy and the upstream's definition matches the pin.</summary>
    Approved,

    /// <summary>Declared in the policy but the upstream's definition has changed. Disabled.</summary>
    Mismatch,

    /// <summary>Declared in the policy with no pin yet. Disabled until approved.</summary>
    NotPinned,

    /// <summary>Declared in the policy but the upstream does not offer it. Disabled.</summary>
    Missing,

    /// <summary>Offered by the upstream, not declared in the policy. Never exposed.</summary>
    Undeclared,
}

public sealed record DiscoveredTool(string Name, string Pin, PinStatus Status);

/// <summary>
/// One upstream MCP server: connection, discovery, pin checks and guarded calls
/// (timeout, one retry on transient failure, circuit breaker).
/// </summary>
public sealed class UpstreamConnection(
    UpstreamConfig config,
    UpstreamPolicy policy,
    IUpstreamClientFactory factory,
    TimeProvider clock,
    ILogger<UpstreamConnection> logger) : IAsyncDisposable
{
    private const int BreakerThreshold = 5;
    private static readonly TimeSpan BreakerPause = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly CircuitBreaker _breaker = new(clock, BreakerThreshold, BreakerPause);
    private IUpstreamClient? _client;
    private bool _hasDiscovered;
    private IReadOnlyDictionary<string, UpstreamToolDefinition> _approved = new Dictionary<string, UpstreamToolDefinition>();
    private IReadOnlyList<DiscoveredTool> _discovered = [];

    public string Name => config.Name;

    public string Layer => policy.Layer;

    public UpstreamPolicy Policy => policy;

    /// <summary>True once discovery has succeeded and the circuit is closed. A dropped connection reconnects on the next call.</summary>
    public bool IsReady => _hasDiscovered && !_breaker.IsOpen;

    public IReadOnlyList<DiscoveredTool> Discovered => _discovered;

    public bool IsToolApproved(string toolName) => _approved.ContainsKey(toolName) && IsReady;

    public UpstreamToolDefinition? GetApprovedDefinition(string toolName) =>
        _approved.TryGetValue(toolName, out var definition) && IsReady ? definition : null;

    public IReadOnlyCollection<UpstreamToolDefinition> ApprovedDefinitions => IsReady ? _approved.Values.ToList() : [];

    /// <summary>Lists the upstream's tools and checks each declared tool against its pin.</summary>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var tools = await ExecuteAsync((client, ct) => client.ListToolsAsync(ct), cancellationToken);

        var approved = new Dictionary<string, UpstreamToolDefinition>(StringComparer.Ordinal);
        var discovered = new List<DiscoveredTool>();

        foreach (var tool in tools)
        {
            var computed = PinCalculator.Compute(tool);
            var status = Classify(tool.Name, computed);
            discovered.Add(new DiscoveredTool(tool.Name, computed, status));

            if (status == PinStatus.Approved)
            {
                approved[tool.Name] = tool;
            }
            else if (status is PinStatus.Mismatch or PinStatus.NotPinned)
            {
                logger.LogWarning(
                    "PIN_MISMATCH upstream {Upstream} tool {Tool} status {Status}. The tool is disabled until it is approved again.",
                    Name, tool.Name, status);
            }
        }

        foreach (var declared in policy.ToolPins.Keys.Where(name => tools.All(t => t.Name != name)))
        {
            discovered.Add(new DiscoveredTool(declared, string.Empty, PinStatus.Missing));
        }

        _approved = approved;
        _discovered = discovered;
        _hasDiscovered = true;
    }

    public async Task<UpstreamCallResult> CallToolAsync(string toolName, JsonObject arguments, CancellationToken cancellationToken) =>
        await ExecuteAsync((client, ct) => client.CallToolAsync(toolName, arguments, ct), cancellationToken);

    public async Task<string?> ReadResourceAsync(string uri, CancellationToken cancellationToken) =>
        await ExecuteAsync((client, ct) => client.ReadResourceTextAsync(uri, ct), cancellationToken);

    private PinStatus Classify(string toolName, string computedPin)
    {
        if (!policy.ToolPins.TryGetValue(toolName, out var declaredPin))
        {
            return PinStatus.Undeclared;
        }

        if (declaredPin is null)
        {
            return PinStatus.NotPinned;
        }

        return string.Equals(declaredPin, computedPin, StringComparison.Ordinal) ? PinStatus.Approved : PinStatus.Mismatch;
    }

    private async Task<T> ExecuteAsync<T>(Func<IUpstreamClient, CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        if (!_breaker.TryAcquire())
        {
            throw new SourceUnavailableException(Name);
        }

        Exception? last = null;

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var client = await GetClientAsync(cancellationToken);

                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));

                var result = await operation(client, timeout.Token);
                _breaker.RecordSuccess();
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                last = ex;
                logger.LogWarning(ex, "Upstream {Upstream} call failed (attempt {Attempt})", Name, attempt);
                await DropClientAsync();
            }
        }

        _breaker.RecordFailure();
        throw new SourceUnavailableException(Name, last);
    }

    private async Task<IUpstreamClient> GetClientAsync(CancellationToken cancellationToken)
    {
        if (_client is not null)
        {
            return _client;
        }

        await _connectLock.WaitAsync(cancellationToken);
        try
        {
            return _client ??= await factory.ConnectAsync(config, cancellationToken);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private async Task DropClientAsync()
    {
        var client = Interlocked.Exchange(ref _client, null);
        if (client is not null)
        {
            try
            {
                await client.DisposeAsync();
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Error while closing upstream {Upstream}", Name);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DropClientAsync();
        _connectLock.Dispose();
    }
}
