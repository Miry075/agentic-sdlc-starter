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
| `--html` | Always write the standalone HTML report, even when output is piped or in CI (see below) |
| `--out report.html` | Write the HTML report to this file instead of a temporary one |
| `--no-open` | Do not open the HTML report in the browser |
| `--cta-url <url>` / `--cta-text <text>` | Add a button to the HTML report, for example your booking page |
| `--all` | Treat the path as a folder of repositories and rank them all (see below) |
| `--fix` | Show what the scanner can add to raise the score, plus prompts for the rest (see below) |
| `--apply` | With `--fix`: create the files |
| `--min-score 60` | Exit code 1 when a score is below 60, for CI. With `--all`: when any repository is below |

Exit codes: `0` ok, `1` below `--min-score`, `2` bad arguments or folder not found, `3` could not apply fixes.

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

## Raise the score: `--fix`

```bash
dotnet run --project scanner/src/AiReadiness.Scanner -- /path/to/repo --fix           # preview, writes nothing
dotnet run --project scanner/src/AiReadiness.Scanner -- /path/to/repo --fix --apply   # create the files
```

The preview has two parts.

**Automatic fixes** are files the scanner can create because their content is true for any repository. The gain of each is measured by scanning the repository as if the files existed, not estimated.

| Fix | Creates | Only when |
|---|---|---|
| Guard secrets from agents | `.claude/settings.json` (deny rules and hook), `.claude/hooks/block-classified.sh`, `.claude/classified-paths.txt` | no `.claude/settings.json` yet |
| Keep `.env` out of Git | `.gitignore` | no `.gitignore` yet |
| First decision record | `docs/adr/0000-template.md`, `docs/adr/0001-record-architecture-decisions.md` (dated today) | no ADRs yet |
| Point CLAUDE.md at AGENTS.md | `CLAUDE.md` | `AGENTS.md` exists, no `CLAUDE.md` |
| `/ai-readiness` skill | `.claude/skills/ai-readiness/SKILL.md` (earns no points; it does the rest, see below) | not installed yet |
| CI checks | `.github/workflows/agent-context.yml` or `.azure-pipelines/agent-context.yml`, plus the two scripts in `scripts/` | `AGENTS.md` exists and the CI system is clear (a `.github` folder, a pipeline file or the Git remote) |

The template files come from this repository (`templates/`, `scripts/`, `ci/`) and are compiled into the tool, so there is one source of truth.

**Guided steps** need someone who knows the system: `AGENTS.md`, the glossary, specs, the evaluation set, further ADRs, and edits to files that already exist (`.gitignore`, `.claude/settings.json`). For each, the scanner prints a prompt to paste into Claude Code in that repository. The prompts tell it to base everything on what it can verify, to write `TODO(human): question` where it cannot, and never to invent facts. The scanner does not write this content itself: an empty or invented glossary would raise the score without making agents any better.

Safety rules:

- Without `--apply`, nothing is written.
- It only creates files. If any target already exists, it writes nothing and exits with code 3.
- It never writes outside the repository or through a symbolic link.
- It does not commit. Review the files and commit them yourself.
- `.claude/hooks/block-classified.sh` needs `jq` on each developer machine.
- The CI checks fail pull requests that change the project structure without updating `AGENTS.md`. That is their purpose, but tell your team first.

Exit codes: `0` ok, `1` below `--min-score`, `2` bad arguments or folder not found, `3` could not apply.

`--fix --json` (and `--fix --apply --json`) prints the plan as JSON: scores, files, notes, and each guided step with its `prompt` and `criteria`. That is what the skill reads.

## Reach the maximum: the `/ai-readiness` skill

`--fix --apply` installs a Claude Code skill in the repository. In Claude Code, in that repository, type `/ai-readiness`. It:

1. checks that the scanner is installed and the Git tree is clean, and creates a branch `ai-readiness/<date>`;
2. applies the safe fixes and commits them;
3. works through the guided steps, largest gain first, following each prompt, re-running `--fix --apply` when a step unlocks new fixes (CLAUDE.md and the CI checks need AGENTS.md first), with one commit per area;
4. collects every `TODO(human)` question and asks them all at once, then fills in your answers;
5. stops when the score stops rising, opens the HTML report and summarises before, after and what is left.

It never invents content, never touches application code, never pushes or merges. Evaluation questions stay out until you confirm them, and specs need real work items: read from Azure DevOps or Jira when such a tool is connected, otherwise from your answers. So the last points always need you. The source is `templates/claude/skills/ai-readiness/SKILL.md`.

## Shareable HTML report

At a terminal, every scan writes the HTML report to a temporary file and opens it in your default browser, next to the markdown summary. You do not pass any option or file name. Add `--no-open` to skip the browser, `--html` to write the report even when nothing would open (piped output, CI), or `--out file.html` to choose where it is written. Nothing opens when the output is piped or redirected, when `--json` is used, or in CI (`CI`, `TF_BUILD` or `GITHUB_ACTIONS` set). If the browser cannot be started, the scan still succeeds and prints the file path. `--fix` never opens a browser.

```bash
dotnet run --project scanner/src/AiReadiness.Scanner -- /path/to/repo \
  --out report.html --cta-url https://example.com/book --cta-text "Book a call"
```

One self-contained file: no scripts, no external requests. It shows the score, the score per area, the next actions and every check, with a light and dark theme. E-mail it, host it, or print it to PDF from the browser. The button only accepts http and https addresses, and every value is HTML-encoded.

## Scan a whole organisation

Clone the repositories into one folder, then scan the folder:

```bash
# GitHub
gh repo list my-org --limit 200 --json name --jq '.[].name' | xargs -I{} gh repo clone my-org/{} repos/{} -- --depth 1
# Azure DevOps
az repos list --org https://dev.azure.com/my-org --project MyProject --query "[].name" -o tsv | xargs -I{} git clone --depth 1 https://dev.azure.com/my-org/MyProject/_git/{} repos/{}

dotnet run --project scanner/src/AiReadiness.Scanner -- repos --all --out org.html
```

`--all` scans every direct subfolder that is a Git repository, and ranks them lowest score first with the biggest gap of each. Shallow clones are enough: the scanner reads files, not history.

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
