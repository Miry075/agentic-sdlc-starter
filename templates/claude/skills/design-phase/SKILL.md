---
name: design-phase
description: Decide how to build an approved spec: touched components, contracts, data changes, the test plan, and an ADR when a structural decision is needed. Use after the spec is merged, or on /design-phase <id>.
---

# Design phase

You turn an approved spec into a design that the build phase can follow without making structural decisions on its own.

## Ground rules (every phase)

- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, …), use them and pass `phase: "design"` on every call. Otherwise read the same knowledge locally: `AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`, and the work item through an Azure DevOps or Jira tool if one is connected. If neither has what you need, ask; do not guess.
- **Honesty.** Never invent business rules, acceptance criteria, decisions or results. Write `TODO(human): the question` and ask.
- **Branch policy.** Work on the work item's branch, created from the default branch (`git symbolic-ref --short refs/remotes/origin/HEAD`, else `main` or `master`). Run `git branch --show-current` before every commit; never commit on the default branch. Commit messages start with the work item id (`#1234: ...`) so the tracker links them.
- **Pull requests.** Never push, merge, or open the pull request yourself, and never change the work item's state or fields. End by printing the PR title, description and commands, and the state change to make in the tracker.
- **Secrets.** Never read, print or copy secrets, `.env` files or credentials.


## Steps

1. Read the spec `docs/specs/<id>.md`. If it is missing or still has open questions that block the design, stop and say so.
2. Create the branch `feature/<id>-<short-slug>` from the default branch (or switch to it if it exists). The build and test phases continue on this branch.
3. Read `AGENTS.md`, the ADRs that touch the area (`search_adrs`, or `docs/adr/`), and the code you will touch and its callers (`search_code_graph`, `get_service_map`, or search the code). Read only what you need.
4. Add a `## Design` section to the spec with:
   - **Components:** the projects, classes and endpoints that change, and new ones, with the layer each belongs to.
   - **Contracts:** API, message and database changes, with backward compatibility and migrations.
   - **Test plan:** for each acceptance criterion, the test that will prove it and its level (unit, integration, end to end).
   - **Risks:** performance, security and data risks, with how the design handles them.
5. If the design needs a structural decision (new dependency, new data store, new contract between services, a change to layering), write an ADR in `docs/adr/` from `0000-template.md` with `Proposed` under `## Status`. Never mark it Accepted yourself.
6. If a rule in the spec contradicts an accepted ADR, stop and report the conflict instead of choosing.
7. Commit: `#<id>: design`.

## Done when

Every acceptance criterion has a planned test, every changed component is named, and structural decisions are written as Proposed ADRs. Print the PR or the next step (`/build-phase <id>` on the same branch).

## You do not have

Production code changes are not part of this phase. Do not start implementing.
