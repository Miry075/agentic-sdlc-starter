---
name: requirements-phase
description: Start a work item the agentic-SDLC way (azure-workitem-flow fetch, steps 1 to 3) - fetch it, create the feature branch, split existing from additional acceptance criteria, analyse what and why, sweep the use cases, and commit the spec. Use on /requirements-phase <id or work item URL>.
---

# Requirements phase: fetch, branch, analyse, use cases

You turn a work item into a reviewed spec: the confirmed regression baseline, the delta to build, and every use case it must hold up under. You describe **what** and **why**, never **how**.

## Companion skills

Load these before the first step. `ai-readiness sdlc` installs them next to this skill in `.claude/skills/`; they may also be available as `anthropic-skills:` or synced from claude.ai (all the same):

- `dotnet-change-guardrails`: process safety per phase (A git safety, B analysis, C implementation, D verification, E commit).
- `dotnet-design-principles`: SOLID, DDD, layering, reuse over duplication, minimal footprint.
- `dotnet-testing-standards`: which tests must exist, at which layer, in the same commit.
- `resolve-review-feedback`: fixing what reviewers flagged on the pull request.

This flow follows `azure-workitem-flow` (also installed) step by step. If a companion skill is missing, apply the short version given in each step. If the repository is not .NET, skip the `dotnet-` skills and follow the repository's own conventions.

## Ground rules (every phase)

- **One work item, one branch:** `feature/<work-item-id>-<slugified-title>` (lowercase, hyphens, about 40 characters), always the `feature/` prefix whatever the item type. Every phase of this work item continues on it. Check `git branch --show-current` before every commit; never commit on the base branch.
- **Pause at every gate.** Never advance until the user has answered the current step. These pauses are where the user reviews, chooses and approves.
- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, ...), use them with `phase: "requirements"` on every call. Otherwise read the same knowledge locally (`AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`) and the work item through the Azure DevOps MCP tools. Ground every claim in the code with `file:line` citations.
- **Honesty.** Never invent requirements, decisions or results. Never report as done, built or passing what you have not observed; label it unverified.
- **Commits:** named files only (never `git add -A` or `git add .`), `git status` and `git diff --stat` shown for approval first, message `<type>: <short summary> (AB#<work-item-id>)`, tests in the same commit as the code they cover.
- **Remote and tracker writes only with explicit approval for that action:** pushing, opening the pull request, setting hours or changing the work item state. Never merge, never vote on your own pull request, never move a work item to Closed.
- **Interactive only.** This flow needs the user at each gate. In an unattended run, stop and say so.
- **Secrets and personal data:** never read, print or copy secrets, `.env` files, credentials, or patient or personal data.


## Fetch the work item (no user input unless it fails)

1. Get the work item with relations, its comments, and one level of linked items, with whichever Azure DevOps MCP tool shape is present (`wit_work_item` with `action: "get"` / `"list_comments"` / `"get_batch"`, or the older `wit_get_work_item`, `wit_list_work_item_comments`, `wit_get_work_items_batch_by_ids`). On failure, show the real error and ask the user to paste title, description and acceptance criteria.
2. Collect acceptance criteria from the field, the description, any "additional AC" section and the comments, plus the `Security Criteria` field. Split them:
   - **Existing AC:** behaviour already implemented. The regression baseline.
   - **Additional AC:** new in this change. The work.
   If the split is unclear, say what you inferred and from what.

## Step 1: create the branch (guardrails phase A)

1. `git status --porcelain` must be empty; otherwise stop and ask (commit, stash or discard).
2. Ask which base branch to start from (for example `main` or `develop`). Update it: `git checkout <base>` then `git pull`, nothing else. If the pull fails, show the commands to run locally and continue once the user confirms.
3. Propose `feature/<id>-<slugified-title>`; check it does not exist (`git rev-parse --verify --quiet <name>`); let the user adjust; create it with `git checkout -b <name>`.

## Step 2: analyse what and why (guardrails phase B)

Run this analysis in a subagent on Opus (Agent tool, `model: "opus"`), with everything fetched and the instruction to read the repository, so it is grounded in the code. It reports, in this order:

- **Existing AC checklist, first:** each criterion as a verifiable item with the code that satisfies it today (`file:line`). A criterion that claims to be implemented but is not is a finding. If there are none, say so in one line.
- **What:** the change in engineering terms, with the code locations it touches.
- **Why:** the business or technical motivation.
- **Additional AC checklist:** the new criteria, each verifiable.

**Gate:** present the existing AC as a list and stop. Do not continue until the user confirms the baseline is complete and correct.

## Step 3: identify the use cases

From the code and both checklists: each actor or caller, edge and boundary conditions (empty or null input, concurrency, volume, already-failed or already-completed states), behaviour elsewhere that depends on this code, and related workflows sharing the path. Each item concrete enough to verify later. Say explicitly which use cases are out of scope.

## Write and commit the spec

Write `docs/specs/<id>.md` from `docs/specs/TEMPLATE.md`, keeping its headings: Goal (what and why), Existing acceptance criteria (with `file:line`), Acceptance criteria (the additional AC, as numbered Given / When / Then), Use cases, Edge cases, Non-goals (including out-of-scope use cases), Security criteria, Business rules involved, Related ADRs, Open questions. Run `scripts/check-docs-templates.sh` if it exists. Commit with approval: `docs: spec for <title> (AB#<id>)`.

## Done when

The baseline is confirmed, the additional AC are verifiable, and the use cases are listed. Next: `/design-phase <id>` on the same branch.
