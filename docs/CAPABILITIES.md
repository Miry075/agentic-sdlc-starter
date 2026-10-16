# Capabilities

Everything this repository can do, in one place. Each section links to the detailed guide.

| Part | What it gives you | Guide |
|---|---|---|
| [Templates](#1-templates) | The files that make a repository readable by AI agents | `templates/` |
| [Policy](#2-phase-access-policy) | Who may read which knowledge, in which SDLC phase | `policy/phase-access.yaml` |
| [Agent guardrails](#3-agent-guardrails-claude-code) | Claude Code settings, a hook that blocks secrets, phase skills | `templates/claude/` |
| [CI checks](#4-ci-checks) | Pull requests fail when agent context goes stale | `ci/`, `scripts/` |
| [Evaluation set](#5-evaluation-set) | Real questions that measure whether the knowledge helps | `evals/` |
| [Knowledge gateway](#6-knowledge-gateway-mcp) | One MCP server between agents and knowledge: policy, redaction, audit | [docs/gateway/README.md](gateway/README.md) |
| [AI-readiness scanner](#7-ai-readiness-scanner) | A score out of 100 per repository, fixes, reports, and an agent that does the rest | [scanner/README.md](../scanner/README.md) |

---

## 1. Templates

Copy into each product repository (`templates/repo/`) or wiki (`templates/wiki/`).

| File | Purpose |
|---|---|
| `AGENTS.md` | Canonical context for every agent: what the repo is, build and test commands, layout, boundaries. |
| `CLAUDE.md` | One line, `@AGENTS.md`, so Claude Code reads the same file as every other agent. |
| `docs/adr/0000-template.md` | Architecture decision record template (Status, Context, Decision, Consequences, Alternatives). |
| `docs/adr/0001-record-architecture-decisions.md` | The standard first decision: record decisions in the repo. |
| `docs/specs/TEMPLATE.md` | One committed spec per work item (Goal, Acceptance criteria, Edge cases, Non-goals, Business rules, Related ADRs). |
| `docs/domain/glossary.md` | Business terms and acronyms, the biggest accuracy gain for the lowest effort. |
| `templates/wiki/ai-ready-page.md` | Template for wiki pages served to agents; only pages labelled `ai-ready` are exposed. |

## 2. Phase access policy

`policy/phase-access.yaml`, versioned in Git and owned by architects and security.

- **Deny by default.** A tool is reachable only when every rule allows it.
- **Effective access** = role ceiling (identity group) ∩ phases allowed by the work-item state ∩ the phase the agent declares. The declared phase can only narrow access, never widen it.
- **Phases:** requirements, design, build, test, release, each with its list of tools.
- **Work-item states** (New, Approved, Active, Resolved, Closed) map to the phases they allow. Closed allows none.
- **Roles** (developers, product-owners, testers) map from Entra ID groups.
- **Field allow-list** for work items: everything else (attachments, custom fields) is dropped.
- **Upstream MCP servers** are declared here too, with tool pins and resource rules (see the gateway).

## 3. Agent guardrails (Claude Code)

`templates/claude/`

| File | What it does |
|---|---|
| `managed-settings.json` | Org-wide settings to deploy through device management: deny rules for `.env`, secrets, seeds, test data, certificates, dumps; only the knowledge MCP server allowed; the hook below. |
| `project-settings.json` | The same deny rules and hook for one repository (`.claude/settings.json`). |
| `hooks/block-classified.sh` | `PreToolUse` hook: blocks Read, Edit, Write, Grep and Glob on paths matching `classified-paths.txt`. Needs `jq`. |
| `classified-paths.txt` | The blocked path patterns, one extended regex per line. |
| `skills/requirements-phase` | Turns a New work item into a committed spec with testable acceptance criteria, through the gateway. |
| `skills/build-phase` | Implements an approved spec: reads AGENTS.md, the spec, ADRs and glossary first; one test per acceptance criterion. |
| `skills/ai-readiness` | Raises a repository's AI-readiness score without inventing content (see the scanner). |

## 4. CI checks

| File | What it checks |
|---|---|
| `scripts/check-agents-md-freshness.sh` | Fails a pull request that changes the project structure without updating an `AGENTS.md`. Skip one PR with `SKIP_AGENTS_MD_CHECK=true`. |
| `scripts/check-docs-templates.sh` | ADRs have Status, Context, Decision and Consequences with a valid status; specs have Goal, Acceptance criteria and Non-goals. |
| `ci/github/agent-context.yml`, `ci/azure-devops/agent-context.yml` | Run both scripts on every pull request (GitHub also checks links in agent docs). |
| `ci/github/ai-readiness.yml`, `ci/azure-devops/ai-readiness.yml` | Run the scanner on every pull request, put the report on the run summary, optional minimum score. |
| `ci/github/gateway-build.yml` | Build and test the gateway on pull requests that touch it. |

## 5. Evaluation set

`evals/questions.yaml`: real questions your team asks, each with the phase, the expected sources and terms a correct answer must contain, and negative tests the agent must refuse. Run it before and after every change to the knowledge layer. When the score drops, fix the source (page, ADR, glossary), never the index. The automated runner is on the roadmap.

## 6. Knowledge gateway (MCP)

A .NET 10 ASP.NET Core service, the single door between agents and knowledge. Details and setup: [docs/gateway/README.md](gateway/README.md). Design and deviations: [docs/gateway/DESIGN.md](gateway/DESIGN.md).

### For agents

- One MCP endpoint, `/mcp` (Streamable HTTP, stateless). Every tool takes a required `phase` argument.
- Built-in tools:

| Tool | Layer | Returns |
|---|---|---|
| `get_glossary_term` | L1 repo context | The definition of a business term from `docs/domain/glossary.md` |
| `search_adrs` | L1 repo context | Matching architecture decision records |
| `get_spec` | L1 repo context | The committed spec for a work item (state-aware) |
| `get_work_item` | L4 work items | Title, description, acceptance criteria, state, comments, links, read live on behalf of the user (state-aware) |

- Tools from connected MCP servers appear as `<upstream>.<tool>` (for example `graphify.query_graph`), and their resources through `<upstream>.read_resource`.

### Every call goes through the same pipeline

1. **Policy check:** role, work-item state, declared phase, upstream pin. Unknown phase, unknown tool or unknown state means deny.
2. **Fetch** from the source.
3. **Redact** sensitive data: connection strings, tokens, secrets and keys, e-mail addresses, IBANs (checksum-validated), national IDs, card numbers (Luhn), phone numbers. Each match is replaced with `[KIND]`. A redaction that cannot finish in time withholds the whole result.
4. **Truncate** to 64K characters.
5. **Audit:** one structured `knowledge.access` event per call, allowed, denied or failed, to the console or Azure Monitor.

### Sources

| Source | Options |
|---|---|
| Repository context (L1) | A folder on disk (relative or absolute path; no path traversal, symlinks ignored) or Azure Repos (managed identity, 60 s cache) |
| Work items (L4) | Azure DevOps, on behalf of the signed-in user (comments included, HTML turned into text, 30 s cache per user) or JSON files for dev mode |
| Upstream MCP servers | Any MCP server over stdio or HTTPS: Graphify, Confluence, others |

### Upstream MCP servers

- Only tools declared in the policy are exposed. Undeclared tools are never visible.
- Each tool is **pinned** by a SHA-256 of its name, description and schema. A changed tool is disabled until re-approved.
- `upstreams list` and `upstreams approve <name>` print the current tools and pins for review; they never edit files.
- Resources are opt-in, limited to URI patterns and phases.
- Circuit breaker (5 failures, 30 s pause), one retry, per-upstream timeout, re-discovery every 10 minutes.
- Upstream auth: none, a Key Vault secret, or managed identity. Per-user (on-behalf-of) auth is planned for v0.2.
- The gateway refuses sampling, elicitation and roots requests from upstreams.

### Identity and operations

- **Entra ID** with group-to-role mapping, or **Dev mode** (fixed user, refuses to start in Production).
- Health endpoints: `/health/live`, `/health/ready`.
- Docker image (non-root, port 8080), policy copied into the image: a policy change is a reviewed pull request and a redeploy.

## 7. AI-readiness scanner

A .NET 10 command-line tool, `ai-readiness`. It reads files only: no network, no AI model, nothing leaves the machine. Full guide: [scanner/README.md](../scanner/README.md).

### Score a repository

```bash
ai-readiness /path/to/repo
```

A score out of 100 in eight areas, with the next actions ranked by points gained:

| Area | Points |
|---|---|
| Agent context (AGENTS.md, CLAUDE.md) | 20 |
| Domain glossary | 15 |
| Architecture decisions | 10 |
| Specs per work item | 10 |
| Keeping context fresh in CI | 10 |
| Sensitive data protection | 20 |
| Evaluation set | 10 |
| Agent settings | 5 |

Bands: 90+ Strong, 70+ Ready, 40+ Partial, below 40 Not ready.

### Reports

| Command | Output |
|---|---|
| `ai-readiness <repo>` | Markdown summary in the terminal, and the **HTML report opens in the browser** automatically |
| `--no-open` | Skip the browser |
| `--html` | Always write the HTML report, even in CI or when piped |
| `--out report.html` | Choose where the HTML report is written |
| `--cta-url <url> --cta-text <text>` | Add a call-to-action button to the HTML report |
| `--json` | Full report as JSON |
| `--min-score 60` | Exit code 1 below 60, to gate pull requests |

The HTML report is one self-contained file (no scripts, no external requests), light and dark theme, printable to PDF.

### Scan a whole organisation

```bash
ai-readiness ~/repos --all
```

Scans every Git repository in the folder and ranks them lowest score first, with the average, the count per band and the biggest gap of each.

### Fix it

```bash
ai-readiness <repo> --fix           # preview, writes nothing
ai-readiness <repo> --fix --apply   # create the files
```

- **Automatic fixes**, only files that are true for any repository: the secret guard (`.claude/settings.json`, hook, patterns), `.gitignore`, the first ADR, `CLAUDE.md`, the CI checks (GitHub or Azure DevOps, detected), and the `/ai-readiness` skill. The gain of each is measured by re-scanning, not estimated.
- **Guided steps** for what needs knowledge of the system (AGENTS.md, glossary, specs, evaluation set, more ADRs, edits to existing files): a ready-to-paste Claude Code prompt that forbids inventing facts.
- Create only, never overwrites, never writes outside the repository or through a symlink, never commits.
- `--fix --json` gives the plan to tools.

### Let Claude Code do the rest

- **In Claude Code:** type `/ai-readiness` in the repository. The skill works on a branch, applies the safe fixes, does the guided steps largest gain first, asks you every open question at once, re-scans until the score stops rising, and opens the report.
- **From the terminal:** `ai-readiness agent <repo>` runs the same skill unattended with your Claude login (`--dry-run` shows the command). Minimum permissions: file edits, the scanner, `dotnet build`/`test`, the template check and local Git commands. No push. It ends with the questions to answer through `claude --continue`, and prints the score before and after.

The last points always need you: real glossary meanings, confirmed evaluation questions and real work items cannot be read from code, and the skill never makes them up.

### Install

```bash
scanner/install.sh          # install, or update after a git pull
ai-readiness --version
```

Runs on macOS, Linux and Windows with the .NET 10 SDK.

### Exit codes

| Code | Meaning |
|---|---|
| 0 | OK |
| 1 | A score is below `--min-score` |
| 2 | Bad arguments, folder not found, not a Git repository (agent), or Claude Code missing |
| 3 | Fixes could not be applied (a target file already exists); nothing was written |
| 4 | The Claude Code run failed |

---

## Not built yet

- Evaluation runner (automated scoring of `evals/questions.yaml`).
- Gateway: L3 product docs adapter (Confluence or Azure DevOps wiki), L2 code graph, per-user auth for upstream MCP servers, several repositories per gateway, GitHub as a repository source.
- Scanner: score trend over time, publishing to NuGet.

## Verification status

The gateway and the scanner compile with the .NET 10 SDK and their tests pass, but they were written where NuGet was unreachable, so the tests ran against stand-ins for xunit and the gateway's MCP, Identity and Azure SDKs. Run `dotnet test` on a machine with NuGet access before relying on them in production.
