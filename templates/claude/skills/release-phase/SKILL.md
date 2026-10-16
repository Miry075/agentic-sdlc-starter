---
name: release-phase
description: Prepare a release from delivered work items - release notes, migrations, configuration and feature-flag checklist, rollback plan and blockers. Never deploys, tags or publishes. Use when preparing a release or on /release-phase <version>.
---

# Release phase

You prepare the release. You never deploy, tag, publish or change environments.

## Companion skills

Load these before the first step when they are available (plain name, `anthropic-skills:` prefix, or synced from claude.ai; all the same):

- `dotnet-change-guardrails`: process safety per phase (A git safety, B analysis, C implementation, D verification, E commit).
- `dotnet-design-principles`: SOLID, DDD, layering, reuse over duplication, minimal footprint.
- `dotnet-testing-standards`: which tests must exist, at which layer, in the same commit.
- `resolve-review-feedback`: fixing what reviewers flagged on the pull request.

This flow follows `azure-workitem-flow` step by step. If a companion skill is missing, apply the short version given in each step. If the repository is not .NET, skip the `dotnet-` skills and follow the repository's own conventions.

## Ground rules (every phase)

- **One work item, one branch:** `feature/<work-item-id>-<slugified-title>` (lowercase, hyphens, about 40 characters), always the `feature/` prefix whatever the item type. Every phase of this work item continues on it. Check `git branch --show-current` before every commit; never commit on the base branch.
- **Pause at every gate.** Never advance until the user has answered the current step. These pauses are where the user reviews, chooses and approves.
- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, ...), use them with `phase: "release"` on every call. Otherwise read the same knowledge locally (`AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`) and the work item through the Azure DevOps MCP tools. Ground every claim in the code with `file:line` citations.
- **Honesty.** Never invent requirements, decisions or results. Never report as done, built or passing what you have not observed; label it unverified.
- **Commits:** named files only (never `git add -A` or `git add .`), `git status` and `git diff --stat` shown for approval first, message `<type>: <short summary> (AB#<work-item-id>)`, tests in the same commit as the code they cover.
- **Remote and tracker writes only with explicit approval for that action:** pushing, opening the pull request, setting hours or changing the work item state. Never merge, never vote on your own pull request, never move a work item to Closed.
- **Interactive only.** This flow needs the user at each gate. In an unattended run, stop and say so.
- **Secrets and personal data:** never read, print or copy secrets, `.env` files, credentials, or patient or personal data.


## Steps

1. Find what is in the release: commits on the base branch since the last tag (`git describe --tags --abbrev=0`, then `git log --oneline <tag>..HEAD`) and the `AB#<id>` work items in them.
2. For each work item, read its spec and `## Verification`. Any item not verified, or verified as unverified, is a blocker.
3. On a branch `feature/release-<version>` from the base branch, write `docs/releases/<version>.md`: release notes per work item in plain language; a checklist of database migrations and their order, configuration and secrets to add (names only, never values), feature flags and infrastructure changes; the ADRs that changed; a rollback plan for each risky change, naming what cannot be undone; the blockers.
4. **Gate:** show it, then commit with approval: `docs: release notes <version>`. The pull request follows `/deliver-phase` rules (ask who opens it).

## Done when

Every work item in the release is listed with its verification status, every migration and configuration change is in the checklist, and every High-risk item has a rollback step. Say clearly whether there are blockers.
