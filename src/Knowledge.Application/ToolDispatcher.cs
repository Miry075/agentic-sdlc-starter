using System.Text.Json.Nodes;
using Knowledge.Core;
using Microsoft.Extensions.Logging;

namespace Knowledge.Application;

/// <summary>
/// Turns an agent's tool request into a guarded call: finds the tool, takes the declared phase out of
/// the arguments, looks up the work-item state, and hands the read to <see cref="IToolGuard"/>.
/// </summary>
public sealed class ToolDispatcher(
    IEnumerable<IToolSource> sources,
    IPhasePolicy policy,
    IToolGuard guard,
    ILogger<ToolDispatcher> logger)
{
    private const string PhaseArgument = "phase";

    /// <summary>Tools the caller may see, each with the required <c>phase</c> argument added.</summary>
    public IReadOnlyList<ToolDescriptor> ListTools(CallerContext caller)
    {
        var visible = policy.ToolsVisibleTo(caller);

        return AllTools()
            .Where(tool => visible.Contains(tool.Descriptor.Name))
            .Select(tool => tool.Descriptor with
            {
                InputSchema = PhaseSchema.WithPhase(tool.Descriptor.InputSchema, policy.Phases),
            })
            .OrderBy(descriptor => descriptor.Name, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ToolResult> CallAsync(
        CallerContext caller,
        string toolName,
        JsonObject? arguments,
        CancellationToken cancellationToken)
    {
        var tool = AllTools().FirstOrDefault(t => t.Descriptor.Name == toolName);
        var (phase, toolArguments) = SplitPhase(arguments);

        if (tool is null)
        {
            // The policy denies unknown tools; going through the guard keeps the denial audited.
            var unknown = new ToolInvocation(caller, toolName, phase, "unknown", null);
            return await guard.RunAsync(unknown, _ => throw new InvalidOperationException("unreachable"), cancellationToken);
        }

        var state = await TryGetWorkItemStateAsync(tool, caller, toolArguments, cancellationToken);
        var descriptor = tool.Descriptor;
        var invocation = new ToolInvocation(caller, descriptor.Name, phase, descriptor.Layer, state, descriptor.Upstream);

        return await guard.RunAsync(
            invocation,
            ct => tool.ReadAsync(caller, toolArguments, ct),
            cancellationToken);
    }

    private IEnumerable<IGatewayTool> AllTools() =>
        sources.SelectMany(source => source.GetTools()).DistinctBy(tool => tool.Descriptor.Name);

    private static (string Phase, JsonObject Arguments) SplitPhase(JsonObject? arguments)
    {
        var copy = arguments is null ? new JsonObject() : (JsonObject)arguments.DeepClone();

        var phase = copy[PhaseArgument] is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : string.Empty;

        copy.Remove(PhaseArgument);
        return (phase, copy);
    }

    private async Task<string?> TryGetWorkItemStateAsync(
        IGatewayTool tool, CallerContext caller, JsonObject arguments, CancellationToken cancellationToken)
    {
        try
        {
            return await tool.GetWorkItemStateAsync(caller, arguments, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // The policy denies a state-dependent tool when the state is unknown, so failing here is safe.
            logger.LogWarning(ex, "Could not determine the work item state for tool {Tool}", tool.Descriptor.Name);
            return null;
        }
    }
}

internal static class PhaseSchema
{
    public static JsonObject WithPhase(JsonObject schema, IEnumerable<string> phases)
    {
        var copy = (JsonObject)schema.DeepClone();
        copy["type"] ??= "object";

        var properties = copy["properties"] as JsonObject ?? new JsonObject();
        properties["phase"] = new JsonObject
        {
            ["type"] = "string",
            ["enum"] = new JsonArray(phases.Order().Select(p => (JsonNode)JsonValue.Create(p)!).ToArray()),
            ["description"] = "The SDLC phase you are working in. It can only narrow what you may read.",
        };
        copy["properties"] = properties;

        var required = copy["required"] as JsonArray ?? new JsonArray();
        if (!required.Any(node => node?.GetValue<string>() == "phase"))
        {
            required.Add("phase");
        }

        copy["required"] = required;
        return copy;
    }
}
