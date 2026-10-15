using Azure.Monitor.OpenTelemetry.AspNetCore;
using Knowledge.Adapters.RepoContext;
using Knowledge.Core;
using Knowledge.Gateway;
using Knowledge.Gateway.Auth;
using Knowledge.Policy;
using Knowledge.Upstreams;

var builder = WebApplication.CreateBuilder(args);

var options = Composition.BindOptions(builder.Configuration, builder.Environment);
builder.Services.AddKnowledgeGateway(options, builder.Configuration);

builder.Logging.AddJsonConsole();
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
}

var app = builder.Build();

if (AdminCommands.IsAdminCommand(args))
{
    return await AdminCommands.RunAsync(args, app.Services, Console.Out);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

app.MapGet("/health/ready", async (IRepoContextSource repo, UpstreamPool pool, CancellationToken cancellationToken) =>
{
    // Ready means the policy is loaded (startup fails otherwise) and the L1 source answers.
    // Upstream status is reported but never blocks readiness.
    try
    {
        await repo.ListFilesAsync("docs", cancellationToken);
    }
    catch (Exception)
    {
        return Results.Json(new { status = "not ready", reason = "repo source unreachable" }, statusCode: 503);
    }

    return Results.Ok(new
    {
        status = "ready",
        upstreams = pool.Connections.Select(c => new { name = c.Name, ready = c.IsReady }),
    });
});

app.MapMcp("/mcp").RequireAuthorization();

app.Logger.LogInformation(
    "Knowledge gateway started in {AuthMode} mode with {Phases} phases", options.Auth.Mode, app.Services.GetRequiredService<PolicyDocument>().Phases.Count);

await app.RunAsync();
return 0;

public partial class Program;
