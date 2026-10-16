using System.Reflection;

namespace AiReadiness.Scanner.Remediation;

public static class AssetNames
{
    public const string ProjectSettings = "assets/project-settings.json";
    public const string HookScript = "assets/block-classified.sh";
    public const string ClassifiedPaths = "assets/classified-paths.txt";
    public const string FreshnessScript = "assets/check-agents-md-freshness.sh";
    public const string DocsTemplateScript = "assets/check-docs-templates.sh";
    public const string GithubWorkflow = "assets/github-agent-context.yml";
    public const string AzurePipeline = "assets/azure-agent-context.yml";
    public const string AdrTemplate = "assets/adr-0000-template.md";
    public const string FirstAdr = "assets/adr-0001-record-architecture-decisions.md";
    public const string ReadinessSkill = "assets/ai-readiness-skill.md";

    public static IReadOnlyList<string> All { get; } =
    [
        ProjectSettings, HookScript, ClassifiedPaths, FreshnessScript, DocsTemplateScript,
        GithubWorkflow, AzurePipeline, AdrTemplate, FirstAdr, ReadinessSkill,
    ];
}

/// <summary>The starter's own template files, compiled into the tool so there is one source of truth.</summary>
public sealed class EmbeddedAssets : IAssets
{
    public string Read(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Embedded asset not found: {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n");
    }
}
