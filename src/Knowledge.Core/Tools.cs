using System.Text.Json.Nodes;

namespace Knowledge.Core;

public sealed record ToolInvocation(
    CallerContext Caller,
    string Tool,
    string Phase,
    string Layer,
    string? WorkItemState,
    string? Upstream = null);

public sealed record ToolResult(bool IsError, string Text)
{
    public static ToolResult Ok(string text) => new(false, text);

    public static ToolResult Error(string message) => new(true, message);
}

/// <summary>
/// The only way source content leaves the gateway:
/// policy, then fetch, then redact, then audit.
/// </summary>
public interface IToolGuard
{
    Task<ToolResult> RunAsync(
        ToolInvocation invocation,
        Func<CancellationToken, Task<string>> fetch,
        CancellationToken cancellationToken);
}

/// <param name="InputSchema">JSON Schema of the tool's own arguments. The gateway adds the required <c>phase</c> argument.</param>
public sealed record ToolDescriptor(
    string Name,
    string Description,
    string Layer,
    JsonObject InputSchema,
    string? Upstream = null);

/// <summary>A tool the gateway can offer. Tools are thin: they read; the guard decides.</summary>
public interface IGatewayTool
{
    ToolDescriptor Descriptor { get; }

    /// <summary>State of the work item the call is about, or null when the tool is not about a work item.</summary>
    Task<string?> GetWorkItemStateAsync(CallerContext caller, JsonObject arguments, CancellationToken cancellationToken) =>
        Task.FromResult<string?>(null);

    /// <summary>Reads raw content. Only called after the policy allowed the call. <c>phase</c> is already removed.</summary>
    Task<string> ReadAsync(CallerContext caller, JsonObject arguments, CancellationToken cancellationToken);
}

public interface IToolSource
{
    IReadOnlyCollection<IGatewayTool> GetTools();
}

/// <summary>The caller's arguments are invalid. The message is safe to show to the agent.</summary>
public sealed class ToolArgumentException(string message) : Exception(message);

/// <summary>A source could not be reached. Shown to the agent as a retryable error.</summary>
public sealed class SourceUnavailableException(string source, Exception? inner = null)
    : Exception($"Source '{source}' is temporarily unavailable.", inner);
