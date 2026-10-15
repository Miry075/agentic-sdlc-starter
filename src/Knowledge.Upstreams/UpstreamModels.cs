using System.Text.Json.Nodes;

namespace Knowledge.Upstreams;

public enum UpstreamTransport
{
    Stdio,
    Http,
}

public enum UpstreamAuthMode
{
    None,
    KeyVaultSecret,
    ManagedIdentity,
    Obo,
}

public sealed class UpstreamAuthConfig
{
    public UpstreamAuthMode Mode { get; set; } = UpstreamAuthMode.None;

    /// <summary>KeyVaultSecret: name of the secret holding the bearer token or API key.</summary>
    public string? Secret { get; set; }

    /// <summary>ManagedIdentity and Obo: the scope (for example api://upstream/.default).</summary>
    public string? Scope { get; set; }
}

/// <summary>How to reach one upstream MCP server. Bound from <c>Upstreams</c> in configuration.</summary>
public sealed class UpstreamConfig
{
    public string Name { get; set; } = string.Empty;
    public UpstreamTransport Transport { get; set; } = UpstreamTransport.Stdio;
    public string? Command { get; set; }
    public List<string> Args { get; set; } = [];
    public string? Url { get; set; }
    public UpstreamAuthConfig Auth { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 15;
}

public sealed record UpstreamToolDefinition(string Name, string? Description, JsonObject InputSchema);

public abstract record UpstreamContent;

public sealed record UpstreamTextContent(string Text) : UpstreamContent;

/// <summary>Anything that is not text (image, audio, embedded blob). Counted, never forwarded.</summary>
public sealed record UpstreamOtherContent(string Kind) : UpstreamContent;

public sealed record UpstreamCallResult(bool IsError, IReadOnlyList<UpstreamContent> Content);

/// <summary>What the gateway needs from an MCP client. The only implementation that touches the SDK is <c>Sdk/SdkUpstreamClient</c>.</summary>
public interface IUpstreamClient : IAsyncDisposable
{
    Task<IReadOnlyList<UpstreamToolDefinition>> ListToolsAsync(CancellationToken cancellationToken);

    Task<UpstreamCallResult> CallToolAsync(string name, JsonObject arguments, CancellationToken cancellationToken);

    /// <summary>Text of the resource, or null when it does not exist or is not text.</summary>
    Task<string?> ReadResourceTextAsync(string uri, CancellationToken cancellationToken);
}

public interface IUpstreamClientFactory
{
    Task<IUpstreamClient> ConnectAsync(UpstreamConfig config, CancellationToken cancellationToken);
}

/// <summary>Where an upstream's static secrets come from (Key Vault through configuration, in Azure).</summary>
public interface IUpstreamSecrets
{
    Task<string?> GetSecretAsync(string name, CancellationToken cancellationToken);
}
