using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Knowledge.Upstreams.Sdk;

/// <summary>
/// Connects to an upstream with the MCP SDK. The client declares no capabilities, so an upstream cannot
/// ask the gateway for sampling, elicitation or roots: those requests are refused by construction.
/// </summary>
public sealed class SdkUpstreamClientFactory(UpstreamAuthorizer authorizer, ILoggerFactory loggers) : IUpstreamClientFactory
{
    public async Task<IUpstreamClient> ConnectAsync(UpstreamConfig config, CancellationToken cancellationToken)
    {
        IClientTransport transport = config.Transport switch
        {
            UpstreamTransport.Stdio => new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = config.Name,
                Command = config.Command!,
                Arguments = config.Args,
            }),
            UpstreamTransport.Http => CreateHttpTransport(config),
            _ => throw new NotSupportedException($"Transport {config.Transport} is not supported."),
        };

        var options = new McpClientOptions
        {
            ClientInfo = new Implementation { Name = "knowledge-gateway", Version = "0.1.0" },
        };

        var client = await McpClient.CreateAsync(transport, options, loggers, cancellationToken);
        return new SdkUpstreamClient(client);
    }

    private HttpClientTransport CreateHttpTransport(UpstreamConfig config)
    {
        var handler = new UpstreamAuthHandler(authorizer, config.Auth) { InnerHandler = new SocketsHttpHandler() };
        var http = new HttpClient(handler);

        return new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(config.Url!),
                Name = config.Name,
                TransportMode = HttpTransportMode.StreamableHttp,
            },
            http,
            loggers,
            ownsHttpClient: true);
    }
}
