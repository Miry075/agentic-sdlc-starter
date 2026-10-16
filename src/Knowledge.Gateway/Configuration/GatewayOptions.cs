using Knowledge.Adapters.AzureDevOps;
using Knowledge.Adapters.RepoContext;
using Knowledge.Redaction;
using Knowledge.Upstreams;

namespace Knowledge.Gateway.Configuration;

public sealed class GatewayOptions
{
    public AuthOptions Auth { get; set; } = new();
    public PolicyOptions Policy { get; set; } = new();
    public RepoOptions Repo { get; set; } = new();
    public WorkItemOptions WorkItems { get; set; } = new();
    public RedactionOptions Redaction { get; set; } = new();
    public List<UpstreamConfig> Upstreams { get; set; } = [];

    /// <summary>Longest result returned to an agent, in characters.</summary>
    public int MaxResultChars { get; set; } = 64 * 1024;
}

public enum AuthMode
{
    Entra,
    Dev,
}

public sealed class AuthOptions
{
    public AuthMode Mode { get; set; } = AuthMode.Entra;

    /// <summary>Entra group object ID to role name.</summary>
    public Dictionary<string, string> RoleMap { get; set; } = [];

    public DevAuthOptions Dev { get; set; } = new();
}

public sealed class DevAuthOptions
{
    public string UserId { get; set; } = "dev@localhost";
    public List<string> Roles { get; set; } = ["developers"];

    /// <summary>Read Azure DevOps and Azure Repos with the developer's `az login` instead of serving local files only.</summary>
    public bool UseAzureCli { get; set; }
}

public sealed class PolicyOptions
{
    public string Path { get; set; } = "policy/phase-access.yaml";
}

public enum RepoSource
{
    FileSystem,
    AzureRepos,
}

public sealed class RepoOptions
{
    public RepoSource Source { get; set; } = RepoSource.FileSystem;
    public string Path { get; set; } = "samples/demo-repo";
    public AzureReposOptions AzureRepos { get; set; } = new();
}

public enum WorkItemSource
{
    File,
    AzureDevOps,
}

public sealed class WorkItemOptions
{
    public WorkItemSource Source { get; set; } = WorkItemSource.AzureDevOps;
    public string Path { get; set; } = "samples/demo-repo/workitems";
    public AzureDevOpsOptions AzureDevOps { get; set; } = new();
    public int StateCacheSeconds { get; set; } = 30;
}

public sealed class RedactionOptions
{
    public List<CustomRedactionPattern> CustomPatterns { get; set; } = [];
}
