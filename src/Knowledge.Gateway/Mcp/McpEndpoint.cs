using System.Text.Json;
using System.Text.Json.Nodes;
using Knowledge.Application;
using Knowledge.Core;
using Knowledge.Gateway.Auth;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Knowledge.Gateway.Mcp;

// This file is the only place in the host that touches the ModelContextProtocol server SDK.
// The SDK is still evolving: if a build error points here after an SDK upgrade, fix it here and nowhere else.

public static class McpEndpoint
{
    /// <summary>
    /// Serves the gateway over MCP Streamable HTTP. Stateless on purpose: every request is authenticated and
    /// handled on its own, which is what on-behalf-of token exchange needs, and the gateway never pushes to clients.
    /// </summary>
    public static IServiceCollection AddGatewayMcp(this IServiceCollection services)
    {
        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "knowledge-gateway", Version = "0.1.0" };
            })
            .WithHttpTransport(options => options.Stateless = true)
            .WithListToolsHandler((context, cancellationToken) =>
            {
                var handlers = context.Services!.GetRequiredService<GatewayMcpHandlers>();
                return ValueTask.FromResult(handlers.ListTools());
            })
            .WithCallToolHandler(async (context, cancellationToken) =>
            {
                var handlers = context.Services!.GetRequiredService<GatewayMcpHandlers>();
                return await handlers.CallToolAsync(context.Params?.Name, context.Params?.Arguments, cancellationToken);
            });

        services.AddSingleton<GatewayMcpHandlers>();
        return services;
    }
}

/// <summary>Maps between MCP messages and the gateway's own types. No decisions are made here.</summary>
public sealed class GatewayMcpHandlers(ToolDispatcher dispatcher, CallerResolver caller)
{
    public ListToolsResult ListTools() => new()
    {
        Tools = dispatcher.ListTools(caller.Current)
            .Select(descriptor => new Tool
            {
                Name = descriptor.Name,
                Description = descriptor.Description,
                InputSchema = JsonSerializer.SerializeToElement(descriptor.InputSchema),
            })
            .ToList(),
    };

    public async Task<CallToolResult> CallToolAsync(
        string? name, IDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
    {
        var result = await dispatcher.CallAsync(caller.Current, name ?? string.Empty, ToJsonObject(arguments), cancellationToken);

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = [new TextContentBlock { Text = result.Text }],
        };
    }

    internal static JsonObject ToJsonObject(IDictionary<string, JsonElement>? arguments)
    {
        var result = new JsonObject();
        foreach (var (key, value) in arguments ?? new Dictionary<string, JsonElement>())
        {
            result[key] = JsonNode.Parse(value.GetRawText());
        }

        return result;
    }
}
