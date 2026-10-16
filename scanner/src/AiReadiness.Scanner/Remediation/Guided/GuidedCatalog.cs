using System.Text;
using AiReadiness.Scanner.Domain;

namespace AiReadiness.Scanner.Remediation.Guided;

/// <summary>Work that needs knowledge of the system. Each step becomes a prompt for Claude Code that tells it to stay factual.</summary>
public sealed record GuidedStep(string Title, Func<Criterion, bool> Covers, Func<IReadOnlyList<Criterion>, string> BuildPrompt);

public static class GuidedCatalog
{
    private const string Honesty = """
        Rules:
        - Base every statement on what you can verify in this repository (code, manifests, pipelines, history). Do not invent commands, names, dates or decisions.
        - Where you cannot verify something, write `TODO(human): the question to answer` (no angle brackets) and list the questions at the end so I can answer them.
        - Do not print secret values.
        """;

    public static IReadOnlyList<GuidedStep> All { get; } =
    [
        new("Write AGENTS.md",
            c => c.Area == "Agent context (AGENTS.md)",
            failing => $"""
                Prepare this repository for AI coding agents: create or improve `AGENTS.md` at the repository root.

                {Honesty}

                It must contain, in under 300 lines:
                1. What this repository is, in one or two sentences.
                2. Build, test and run commands, exactly as they work here (for example `dotnet build`, `dotnet test`).
                3. A table of the main folders and what each holds, plus the dependency direction between layers.
                4. Boundaries: a section saying what agents must never do (touch secrets, edit generated code, change applied migrations), starting from what this repository shows.
                5. A header line `Owner: <team>` and `Last reviewed: <today as YYYY-MM-DD>`, filled in with real values.

                If `CLAUDE.md` does not exist, create it containing only `@AGENTS.md`. If it exists, add that line at the top.

                Criteria to satisfy:
                {Bullets(failing)}
                """),

        new("Write the domain glossary",
            c => c.Area == "Domain glossary",
            failing => $"""
                Create `docs/domain/glossary.md`, the list of business terms and acronyms agents get wrong most.

                {Honesty}

                Format: a markdown table with the columns Term, Definition, In code, Not to be confused with. Start each term cell with the term in bold, for example `| **Credit note** | ... |`.
                Find the terms in entity and aggregate names, API routes, table names, enum values, comments and the README. Aim for at least 15 terms; at least 5 with a definition you can support from the repository. For terms whose business meaning the code does not explain, write `TODO(human): what does this mean for the business` instead of guessing.

                Criteria to satisfy:
                {Bullets(failing)}
                """),

        new("Write the first specs",
            c => c.Area == "Specs per work item",
            failing => $"""
                Create `docs/specs/<work item id>.md` (for example `docs/specs/1234.md`) for the work items currently in progress: use the ids in recent branch names and commit messages (`git log`, `git branch -a`). If you cannot find any, ask me for the ids.

                {Honesty}

                Each spec starts with `# <work item id>: <title>` and has these `##` headings, in this order: Goal, Acceptance criteria (numbered and testable), Edge cases, Non-goals, Business rules involved, Related ADRs. The CI check `scripts/check-docs-templates.sh` fails without Goal, Acceptance criteria and Non-goals. Take the content from the work item if you can read it, otherwise from the commits and code, and mark the rest `TODO(human): ...`.
                Also create `docs/specs/TEMPLATE.md` with those headings if it does not exist.

                Criteria to satisfy:
                {Bullets(failing)}
                """),

        new("Write the evaluation set",
            c => c.Area == "Measured quality (evaluation set)",
            failing => $"""
                Create `evals/questions.yaml`: the questions a new developer or an agent must be able to answer correctly from this repository's knowledge.

                {Honesty}

                Format:
                ```yaml
                version: 1
                scoring:
                  passThreshold: 0.8
                questions:
                  - id: q001
                    phase: requirements   # requirements | design | build | test | release
                    question: "..."
                    expectedSources: [docs/domain/glossary.md]
                    mustContain: ["term that must appear in a correct answer"]
                ```
                Draft 10 questions answered by files that exist in this repository (expectedSources must be real paths), then ask me to confirm or replace each one: the questions must be real ones my team asks. Grow to 30 later.

                Criteria to satisfy:
                {Bullets(failing)}
                """),

        new("Record more architecture decisions",
            c => c.Area == "Architecture decisions (ADRs)",
            failing => $"""
                Write architecture decision records in `docs/adr/`, numbered sequentially, using `docs/adr/0000-template.md`.

                {Honesty}

                Write up to three ADRs for decisions already visible in the code: data store, messaging, authentication, layering. Each ADR needs the headings `## Status`, `## Context`, `## Decision`, `## Consequences`. Under `## Status`, write `Accepted` on its own line only for decisions that are clearly in force; write `Proposed` when unsure and list them for me to confirm. The CI check `scripts/check-docs-templates.sh` fails otherwise.

                Criteria to satisfy:
                {Bullets(failing)}
                """),

        new("Add .env to .gitignore",
            c => c.Name.StartsWith(".gitignore excludes", StringComparison.Ordinal),
            _ => """
                Add these lines to the existing `.gitignore`, then run `git status` to confirm no `.env` file is tracked:

                ```text
                .env
                .env.*
                !.env.example
                ```
                """),

        new("Merge the agent guard into .claude/settings.json",
            c => c.Area == "Agent settings" || c.Name.StartsWith("Agent deny rules", StringComparison.Ordinal),
            failing => $"""
                `.claude/settings.json` already exists, so merge instead of replacing it. Add the `permissions.deny` rules and the `PreToolUse` hook from the starter's `templates/claude/project-settings.json` (https://github.com/Miry075/agentic-sdlc-starter), keep every existing setting, and create `.claude/hooks/block-classified.sh` (executable) and `.claude/classified-paths.txt` from `templates/claude/` if they are missing. The hook needs `jq`.

                Criteria to satisfy:
                {Bullets(failing)}
                """),

        new("Remove secrets from the repository",
            c => c.Name.StartsWith("No secret files", StringComparison.Ordinal) || c.Name.StartsWith("No passwords", StringComparison.Ordinal),
            failing => $"""
                Remove secrets from this repository. Do not print their values.

                For each finding: replace literal passwords and keys in config files with placeholders such as `<from-key-vault>`, move the real values to Azure Key Vault or user secrets, run `git rm --cached` on committed secret files and add them to `.gitignore`.
                Tell me which credentials must be rotated: anything ever committed is in Git history.

                Findings:
                {Bullets(failing, includeAdvice: true)}
                """),

        new("Add the CI checks",
            c => c.Area == "Keeping context fresh in CI",
            failing => $"""
                Add CI checks that fail a pull request when the project structure changes without updating AGENTS.md, and when ADRs or specs do not follow their template. Use the scripts and pipeline files from https://github.com/Miry075/agentic-sdlc-starter: `scripts/check-agents-md-freshness.sh`, `scripts/check-docs-templates.sh`, and `ci/github/agent-context.yml` or `ci/azure-devops/agent-context.yml`, matching the CI system this repository uses.
                AGENTS.md must exist first.

                Criteria to satisfy:
                {Bullets(failing)}
                """),
    ];

    private static string Bullets(IEnumerable<Criterion> criteria, bool includeAdvice = false)
    {
        var sb = new StringBuilder();
        foreach (var c in criteria)
        {
            sb.AppendLine(includeAdvice ? $"- {c.Name}: {c.Advice}" : $"- {c.Name} (+{c.Weight})");
        }

        return sb.ToString().TrimEnd();
    }
}
