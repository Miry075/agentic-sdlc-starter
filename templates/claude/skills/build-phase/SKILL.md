---
name: build-phase
description: Implement the chosen option with its tests (azure-workitem-flow step 5) - after an explicit go-ahead, holding scope, following local conventions and the design principles, tests written in the same step. Use after /design-phase, or on /build-phase <id>.
---

# Build phase: implementation and tests

You implement exactly the option the user chose, with the tests that prove it. You do not commit yet: review comes first.

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
- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, ...), use them with `phase: "build"` on every call. Otherwise read the same knowledge locally (`AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`) and the work item through the Azure DevOps MCP tools. Ground every claim in the code with `file:line` citations.
- **Honesty.** Never invent requirements, decisions or results. Never report as done, built or passing what you have not observed; label it unverified.
- **Commits:** named files only (never `git add -A` or `git add .`), `git status` and `git diff --stat` shown for approval first, message `<type>: <short summary> (AB#<work-item-id>)`, tests in the same commit as the code they cover.
- **Remote and tracker writes only with explicit approval for that action:** pushing, opening the pull request, setting hours or changing the work item state. Never merge, never vote on your own pull request, never move a work item to Closed.
- **Interactive only.** This flow needs the user at each gate. In an unattended run, stop and say so.
- **Secrets and personal data:** never read, print or copy secrets, `.env` files, credentials, or patient or personal data.


## Step 5: get the go-ahead, then implement (guardrails phase C)

1. Be on `feature/<id>-...`. Read `AGENTS.md`, the spec and its `## Design` section. If there is no chosen option, stop and run `/design-phase <id>` first.
2. **Gate:** ask the user to confirm you should implement the chosen option now. Write no code before the answer.
3. Implement in this session:
   - **Hold scope:** only the chosen option; no renames, reformatting, package bumps or drive-by refactors. Park anything else you notice as follow-up.
   - **Local convention first:** before introducing a pattern, find a nearby example (DI registration, async and `CancellationToken`, logging, errors, nullable context, validation, the test framework in use).
   - **Design principles** in the code you write, whatever surrounds it, with minimal footprint: no new architecture the change does not need. Name nearby violations as follow-up rather than copying or fixing them.
   - **Glossary terms** exactly in class, method and variable names.
4. Write the tests now, not after (`dotnet-testing-standards`): one or more per additional AC and the use cases it maps to, a test pinning each existing AC the change puts at risk, and for a bug fix the failing test first. No mocks of the domain; fakes at the ports the application owns; real infrastructure for infrastructure tests.
5. If the project structure changed (new project, moved folder, new build step), update `AGENTS.md` too.

## Done when

The chosen option and its tests are written and nothing is committed. Next: `/test-phase <id>` to verify, review and commit.
