# AI-readiness scanner

Scores a repository out of 100 on how ready it is for AI coding agents, and lists the next actions by points gained. It checks what the starter templates set up: agent context, glossary, ADRs, specs, CI freshness checks, sensitive-data protection, an evaluation set and agent settings.

It reads files only. It does not call any service, send anything anywhere, or need an AI model.

## Run it

Needs the .NET 10 SDK.

```bash
dotnet run --project scanner/src/AiReadiness.Scanner -- /path/to/your/repo
```

The path can be relative or absolute and defaults to the current folder.

| Option | Effect |
|---|---|
| `--json` | Full report as JSON, including every criterion |
| `--min-score 60` | Exit code 1 when the score is below 60, for CI |

Exit codes: `0` ok, `1` below `--min-score`, `2` bad arguments or folder not found.

Example output:

```text
# AI-readiness: 50/100 (Partial)

| Area | Score |
|---|---|
| Agent context (AGENTS.md) | 18/20 |
| Domain glossary | 11/15 |
...
## Next actions, biggest gain first

1. **+5** At least one committed spec in docs/specs. Commit a spec per work item ...
```

## What is scored

| Area | Points | Looks for |
|---|---|---|
| Agent context | 20 | `AGENTS.md` with build and test commands, boundaries, no template placeholders, 300 lines or fewer; `CLAUDE.md` with `@AGENTS.md` |
| Domain glossary | 15 | `docs/domain/glossary.md` with 5 and 15 real terms |
| Architecture decisions | 10 | `docs/adr/`: at least 1 and 3 ADRs, one accepted |
| Specs | 10 | `docs/specs/<work item id>.md` with acceptance criteria |
| Keeping context fresh | 10 | A CI pipeline that runs the AGENTS.md and docs-template checks |
| Sensitive data | 20 | No committed `.env`, `.pfx` or keys; no passwords in config files; `.env` in `.gitignore`; agent deny rules |
| Evaluation set | 10 | `evals/questions.yaml` with 10 and 30 questions |
| Agent settings | 5 | `.claude/settings.json` with a `PreToolUse` hook |

Bands: 90 and above Strong, 70 Ready, 40 Partial, below 40 Not ready.

An empty repository scores 14, not 0: having no leaked secrets earns points by default. The score is a prioritised to-do list, not a verdict.

## Safety

- Never reads outside the folder you give it, and never follows symbolic links.
- Skips `.git`, `bin`, `obj`, `node_modules`.
- Ignores files over 1 MB.
- Secret findings name the file, never print the value.

## In CI

Copy `ci/github/ai-readiness.yml` or `ci/azure-devops/ai-readiness.yml`. Each runs the scanner on every pull request and puts the report on the run summary.

## Tests

```bash
dotnet test scanner/tests/AiReadiness.Scanner.Tests
```
