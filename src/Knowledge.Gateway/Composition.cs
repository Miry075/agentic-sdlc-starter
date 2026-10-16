using Knowledge.Adapters.AzureDevOps;
using Knowledge.Adapters.Dev;
using Knowledge.Adapters.RepoContext;
using Knowledge.Application;
using Knowledge.Application.Tools;
using Knowledge.Application.WorkItems;
using Knowledge.Core;
using Knowledge.Gateway.Auth;
using Knowledge.Gateway.Configuration;
using Knowledge.Gateway.Mcp;
using Knowledge.Gateway.Telemetry;
using Knowledge.Policy;
using Knowledge.Redaction;
using Knowledge.Upstreams;
using Knowledge.Upstreams.Sdk;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;

namespace Knowledge.Gateway;

/// <summary>The composition root: the only place that knows which implementation serves which contract.</summary>
public static class Composition
{
    public static GatewayOptions BindOptions(IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.Get<GatewayOptions>() ?? new GatewayOptions();

        if (options.Auth.Mode == AuthMode.Dev && environment.IsProduction())
        {
            throw new InvalidOperationException(
                "Auth:Mode 'Dev' is not allowed when ASPNETCORE_ENVIRONMENT is Production. Use 'Entra'.");
        }

        var upstreamErrors = UpstreamConfigValidator.Validate(options.Upstreams);
        if (upstreamErrors.Count > 0)
        {
            throw new InvalidOperationException("Invalid upstream configuration: " + string.Join(" ", upstreamErrors));
        }

        var roots = new[] { environment.ContentRootPath, AppContext.BaseDirectory };
        options.Policy.Path = PathResolver.Resolve(options.Policy.Path, roots);
        options.Repo.Path = PathResolver.Resolve(options.Repo.Path, roots);
        options.WorkItems.Path = PathResolver.Resolve(options.WorkItems.Path, roots);

        return options;
    }

    public static IServiceCollection AddKnowledgeGateway(
        this IServiceCollection services, GatewayOptions options, IConfiguration configuration)
    {
        var policy = PolicyLoader.LoadFromFile(options.Policy.Path);
        RejectUpstreamsMissingFromPolicy(options, policy);

        services.AddSingleton(options);
        services.AddSingleton(options.Auth);
        services.AddSingleton(policy);
        services.AddSingleton(TimeProvider.System);
        services.AddHttpContextAccessor();

        AddAuthentication(services, options, configuration);
        AddUpstreams(services, options, policy);
        AddCore(services, options, policy);
        AddSources(services, options, policy);
        AddTools(services);

        services.AddHostedService<UpstreamRefreshService>();
        services.AddGatewayMcp();
        return services;
    }

    private static void RejectUpstreamsMissingFromPolicy(GatewayOptions options, PolicyDocument policy)
    {
        var missing = options.Upstreams.Select(u => u.Name).Where(name => !policy.Upstreams.ContainsKey(name)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Upstream(s) {string.Join(", ", missing)} are configured but not declared in the policy. " +
                "Declare their tools (with pins) in the policy file, or remove them from configuration.");
        }
    }

    private static void AddAuthentication(IServiceCollection services, GatewayOptions options, IConfiguration configuration)
    {
        services.AddSingleton<CallerFactory>();
        services.AddSingleton<CallerResolver>();
        services.AddAuthorization();

        if (options.Auth.Mode == AuthMode.Dev)
        {
            services.AddAuthentication(DevAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(DevAuthenticationHandler.SchemeName, _ => { });
            if (options.Auth.Dev.UseAzureCli)
            {
                services.AddSingleton<AzureCliTokenProvider>();
                services.AddSingleton<IDownstreamTokenProvider>(sp => sp.GetRequiredService<AzureCliTokenProvider>());
                services.AddSingleton<IServiceTokenProvider>(sp => sp.GetRequiredService<AzureCliTokenProvider>());
                return;
            }

            services.AddSingleton<NoTokenProvider>();
            services.AddSingleton<IDownstreamTokenProvider>(sp => sp.GetRequiredService<NoTokenProvider>());
            services.AddSingleton<IServiceTokenProvider>(sp => sp.GetRequiredService<NoTokenProvider>());
            return;
        }

        services.AddAuthentication("Bearer")
            .AddMicrosoftIdentityWebApi(configuration.GetSection("Auth:Entra"))
            .EnableTokenAcquisitionToCallDownstreamApi()
            .AddInMemoryTokenCaches();

        services.AddSingleton<IDownstreamTokenProvider, EntraOboTokenProvider>();
        services.AddSingleton<IServiceTokenProvider, ManagedIdentityTokenProvider>();
    }

    private static void AddUpstreams(IServiceCollection services, GatewayOptions options, PolicyDocument policy)
    {
        services.AddSingleton<IUpstreamSecrets, ConfigurationUpstreamSecrets>();
        services.AddSingleton<UpstreamAuthorizer>();
        services.AddSingleton<IUpstreamClientFactory, SdkUpstreamClientFactory>();

        services.AddSingleton(sp => new UpstreamPool(
            options.Upstreams.Select(config => new UpstreamConnection(
                config,
                policy.Upstreams[config.Name],
                sp.GetRequiredService<IUpstreamClientFactory>(),
                sp.GetRequiredService<TimeProvider>(),
                sp.GetRequiredService<ILogger<UpstreamConnection>>())),
            sp.GetRequiredService<ILoggerFactory>()));

        services.AddSingleton<IToolSource>(sp => sp.GetRequiredService<UpstreamPool>());
        services.AddSingleton<IToolPinStatus>(sp => sp.GetRequiredService<UpstreamPool>());
    }

    private static void AddCore(IServiceCollection services, GatewayOptions options, PolicyDocument policy)
    {
        services.AddSingleton<IPhasePolicy>(sp => new PhasePolicy(policy, sp.GetRequiredService<IToolPinStatus>()));
        services.AddSingleton<IDataRedactor>(_ => PatternRedactor.Create(options.Redaction.CustomPatterns));
        services.AddSingleton<IAuditLog, LoggerAuditLog>();
        services.AddSingleton(new ToolGuardOptions { MaxResultChars = options.MaxResultChars });
        services.AddSingleton<IToolGuard, ToolGuard>();
        services.AddSingleton<ToolDispatcher>();
        services.AddSingleton<IToolSource, NativeToolSource>();
    }

    private static void AddSources(IServiceCollection services, GatewayOptions options, PolicyDocument policy)
    {
        if (options.Repo.Source == RepoSource.FileSystem)
        {
            services.AddSingleton<IRepoContextSource>(_ => new FileSystemRepoSource(options.Repo.Path));
        }
        else
        {
            services.AddSingleton(options.Repo.AzureRepos);
            services.AddHttpClient<IRepoContextSource, AzureReposSource>();
        }

        if (options.WorkItems.Source == WorkItemSource.File)
        {
            services.AddSingleton<IWorkItemSource>(sp => Cached(sp, new FileWorkItemSource(options.WorkItems.Path), options));
        }
        else
        {
            services.AddSingleton(options.WorkItems.AzureDevOps);
            services.AddHttpClient<AzureDevOpsWorkItemSource>();
            services.AddSingleton<IWorkItemSource>(sp =>
                Cached(sp, sp.GetRequiredService<AzureDevOpsWorkItemSource>(), options));
        }

        services.AddSingleton(new WorkItemFormatter(policy.WorkItemFieldAllowlist));
    }

    // The state lookup for the policy and the read for the result then make one call to the tracker.
    private static IWorkItemSource Cached(IServiceProvider sp, IWorkItemSource inner, GatewayOptions options) =>
        new CachingWorkItemSource(
            inner, sp.GetRequiredService<TimeProvider>(), TimeSpan.FromSeconds(options.WorkItems.StateCacheSeconds));

    private static void AddTools(IServiceCollection services)
    {
        services.AddSingleton<IGatewayTool, GetGlossaryTermTool>();
        services.AddSingleton<IGatewayTool, SearchAdrsTool>();
        services.AddSingleton<IGatewayTool, GetSpecTool>();
        services.AddSingleton<IGatewayTool, GetWorkItemTool>();
    }
}
