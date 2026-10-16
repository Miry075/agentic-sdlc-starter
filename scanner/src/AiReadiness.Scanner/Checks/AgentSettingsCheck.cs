using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Checks;

public sealed class AgentSettingsCheck : IReadinessCheck
{
    public string Area => "Agent settings";

    public IReadOnlyList<Criterion> Evaluate(IRepoFiles repo)
    {
        var settings = repo.ReadText(".claude/settings.json");

        return
        [
            new(Area, ".claude/settings.json exists", 2, settings is not null,
                "Add .claude/settings.json so every developer's agent starts with the same rules."),
            new(Area, "A PreToolUse hook guards file access", 3,
                settings is not null && settings.Contains("PreToolUse", StringComparison.Ordinal),
                "Add a PreToolUse hook that blocks classified paths. The starter ships one in templates/claude/hooks."),
        ];
    }
}
