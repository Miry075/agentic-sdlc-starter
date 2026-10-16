---
name: design-phase
description: Propose and choose how to build a work item (azure-workitem-flow step 4) - 2 to 3 real options assessed on use cases, existing AC, design fit and testability, the user's choice recorded in the spec, and a Proposed ADR for structural decisions. Use after /requirements-phase, or on /design-phase <id>.
---

# Design phase: solution options

You put the real options in front of the user and record the one they choose. You do not write production code.

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
- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, ...), use them with `phase: "design"` on every call. Otherwise read the same knowledge locally (`AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`) and the work item through the Azure DevOps MCP tools. Ground every claim in the code with `file:line` citations.
- **Honesty.** Never invent requirements, decisions or results. Never report as done, built or passing what you have not observed; label it unverified.
- **Commits:** named files only (never `git add -A` or `git add .`), `git status` and `git diff --stat` shown for approval first, message `<type>: <short summary> (AB#<work-item-id>)`, tests in the same commit as the code they cover.
- **Remote and tracker writes only with explicit approval for that action:** pushing, opening the pull request, setting hours or changing the work item state. Never merge, never vote on your own pull request, never move a work item to Closed.
- **Interactive only.** This flow needs the user at each gate. In an unattended run, stop and say so.
- **Secrets and personal data:** never read, print or copy secrets, `.env` files, credentials, or patient or personal data.


## Step 4: propose solution options (guardrails phase B, design principles, testing standards)

1. Be on `feature/<id>-...` and read `docs/specs/<id>.md`: both AC checklists and the use cases. If the spec is missing or has blocking open questions, stop.
2. Read `AGENTS.md`, the ADRs that touch the area, and the code you will change and its callers. Find an existing example of each pattern you plan to use.
3. Give 2 to 3 realistic options when they genuinely exist, or one clearly argued option when they do not. Never pad with straw options; name discarded ones in a line each. For each option:
   - what it involves, and its main trade-off (complexity, risk, performance, maintainability, effort);
   - how it holds up against every use case, and whether it preserves every existing AC: say plainly when it fails one;
   - **design fit** (`dotnet-design-principles`): which SOLID principle or DDD boundary it respects or strains, where the logic lands in the layers, whether it reuses or duplicates existing code. Domain logic in a service, or Domain reaching into Infrastructure, makes it a worse option;
   - **testability** (`dotnet-testing-standards`): the tests it needs and whether they can be written without heavy mocking or new seams.
4. Recommend the simplest option that meets every use case, every existing AC, and the design and testing bars, with a one-line reason.

**Gate:** stop and let the user choose (AskUserQuestion works well).

## Record the choice

Add a `## Design` section to the spec: the chosen option, why, the rejected options in a line each, the components and layers that change, contract or data changes with migrations, and the test plan (each AC and use case mapped to a test and its layer). If the choice is a structural decision (new dependency or data store, new contract between services, a layering change), also write an ADR from `docs/adr/0000-template.md` with `Proposed` under `## Status`; never mark it Accepted yourself. Commit with approval: `docs: design for <title> (AB#<id>)`.

## Done when

The user chose, and every AC and use case has a planned test. Next: `/build-phase <id>`.
