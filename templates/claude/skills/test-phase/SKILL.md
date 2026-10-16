---
name: test-phase
description: Verify and review the implementation, then commit (azure-workitem-flow steps 6 and 7) - build and test for real, walk the existing AC first, then the additional AC and use cases, run the health-sector sweep, report design and coverage honestly, and commit only named files after approval. Use after /build-phase, or on /test-phase <id>.
---

# Test phase: verification, review, commit

You check the change against the spec before anything is committed, and report what is true, including what you could not verify.

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
- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, ...), use them with `phase: "test"` on every call. Otherwise read the same knowledge locally (`AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`) and the work item through the Azure DevOps MCP tools. Ground every claim in the code with `file:line` citations.
- **Honesty.** Never invent requirements, decisions or results. Never report as done, built or passing what you have not observed; label it unverified.
- **Commits:** named files only (never `git add -A` or `git add .`), `git status` and `git diff --stat` shown for approval first, message `<type>: <short summary> (AB#<work-item-id>)`, tests in the same commit as the code they cover.
- **Remote and tracker writes only with explicit approval for that action:** pushing, opening the pull request, setting hours or changing the work item state. Never merge, never vote on your own pull request, never move a work item to Closed.
- **Interactive only.** This flow needs the user at each gate. In an unattended run, stop and say so.
- **Secrets and personal data:** never read, print or copy secrets, `.env` files, credentials, or patient or personal data.


## Step 6: review the implementation (guardrails phase D)

1. Run it, do not claim it: the build and test commands from `AGENTS.md` (for .NET: `dotnet build --nologo`, `dotnet test --nologo`). Report the real outcome. If the environment cannot build or test, label the whole assessment **unverified, not built** and say why.
2. Walk, item by item, marking met, not met, or not verifiable:
   - **Existing AC first:** each baseline criterion still holds. A met delta on top of a regression is not a success.
   - **Additional AC:** each one, with the test that proves it.
   - **Use cases:** each item from the spec.
3. **Health-sector sweep** for regulated code, reported every time even when nothing is found: personal or patient data in logs, traces, exceptions or telemetry; audit trail for actions that must be audited; retention or deletion changes; data crossing a region or tenant boundary; whether new endpoints, handlers or jobs inherit the intended authorization.
4. Report honestly: what you did and how it maps to the chosen option; what still has to happen (configuration, migrations, documentation, follow-up work items); side effects and risks (breaking changes, performance, security, compatibility); which design principles mattered and how the code meets them; test coverage per layer and anything left untested, with the reason. Say so in a line when an area is empty.
5. If a test fails because the production code is wrong, go back to `/build-phase <id>`; do not paper over it.
6. Add a `## Verification` section to the spec: the three walks with results, the commands run, the date, and the sweep result.

## Step 7: commit (guardrails phase E)

**Gate:** ask the user to confirm everything is good to commit. Then show `git status` and `git diff --stat`, stage named files only, and commit: `<type>: <short summary> (AB#<id>)`, with a body explaining why when the diff does not. One commit unless the change has genuinely separable parts. Tests go in the same commit as the code they cover. No push here.

## Done when

The work is verified (or clearly labelled unverified) and committed. Next: `/deliver-phase <id>`.
