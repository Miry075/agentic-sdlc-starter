using Knowledge.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Knowledge.Gateway.IntegrationTests;

/// <summary>The real gateway host in dev mode, serving the sample repository, with an in-memory audit log.</summary>
public class GatewayFactory : WebApplicationFactory<Program>
{
    protected virtual string EnvironmentName => "Development";

    public InMemoryAuditLog Audit { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);

        // UseSetting (not ConfigureAppConfiguration): Program.cs reads the configuration while it builds the host.
        builder.UseSetting("Auth:Mode", "Dev");
        builder.UseSetting("Auth:Dev:UserId", "dev@localhost");
        builder.UseSetting("Auth:Dev:Roles:0", "developers");
        builder.UseSetting("Policy:Path", RepoFile("policy/phase-access.yaml"));
        builder.UseSetting("Repo:Source", "FileSystem");
        builder.UseSetting("Repo:Path", RepoFile("samples/demo-repo"));
        builder.UseSetting("WorkItems:Source", "File");
        builder.UseSetting("WorkItems:Path", RepoFile("samples/demo-repo/workitems"));

        builder.ConfigureTestServices(services => services.AddSingleton<IAuditLog>(Audit));
    }

    public static string RepoFile(string relativePath)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate) || Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not find '{relativePath}' above '{AppContext.BaseDirectory}'.");
    }
}

/// <summary>Dev mode reading Azure DevOps with the developer's Azure CLI sign-in.</summary>
public sealed class AzureCliGatewayFactory : GatewayFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Auth:Dev:UseAzureCli", "true");
    }
}

/// <summary>The same host in Production, where dev authentication must be refused.</summary>
public sealed class ProductionGatewayFactory : GatewayFactory
{
    protected override string EnvironmentName => "Production";
}

public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly List<AuditEvent> _events = [];

    public IReadOnlyList<AuditEvent> Events
    {
        get
        {
            lock (_events)
            {
                return _events.ToList();
            }
        }
    }

    public void Write(AuditEvent auditEvent)
    {
        lock (_events)
        {
            _events.Add(auditEvent);
        }
    }
}
