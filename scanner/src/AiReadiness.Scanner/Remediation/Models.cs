using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Remediation;

/// <summary>A file to create. Existing files are never changed.</summary>
public sealed record FileChange(string Path, string Content, bool Executable = false);

/// <summary>What a fix wants to do, before the planner measures what it earns.</summary>
public sealed record ProposedFix(string Title, IReadOnlyList<FileChange> Changes, IReadOnlyList<string> Notes);

/// <summary>A fix the scanner can apply itself. <see cref="Gain"/> is measured by scanning the repository with the fix applied.</summary>
public sealed record AutomaticFix(string Title, IReadOnlyList<FileChange> Changes, IReadOnlyList<string> Notes, int Gain);

/// <summary>Work that needs someone who knows the system. <see cref="Prompt"/> is ready to paste into Claude Code.</summary>
public sealed record GuidedFix(string Title, string Prompt, int PotentialGain);

public sealed record RemediationPlan(
    Assessment Before,
    Assessment After,
    IReadOnlyList<AutomaticFix> Automatic,
    IReadOnlyList<GuidedFix> Guided)
{
    public IReadOnlyList<FileChange> AllChanges => Automatic.SelectMany(f => f.Changes).ToList();

    /// <summary>The best score reachable if every guided step is done well.</summary>
    public int PotentialScore => Math.Min(After.MaxScore, After.Score + Guided.Sum(g => g.PotentialGain));
}

/// <summary>Everything a fix may look at. Embedded template files come through <see cref="Assets"/>.</summary>
public sealed record RemediationContext(IRepoFiles Repo, Assessment Assessment, DateOnly Today, IAssets Assets);

public interface IAssets
{
    string Read(string name);
}

public interface IAutomaticRemediation
{
    /// <summary>Null when there is nothing safe to add.</summary>
    ProposedFix? Propose(RemediationContext context);
}

public interface IRepoWriter
{
    /// <summary>Creates every file, or none: fails before writing if any target already exists.</summary>
    /// <exception cref="FixConflictException">A target already exists or is outside the repository.</exception>
    void CreateAll(IReadOnlyList<FileChange> changes);
}

public sealed class FixConflictException(string message) : Exception(message);
