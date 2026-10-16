---
name: build-phase
description: Implement a work item whose spec (and design) is approved, with one test per acceptance criterion. Use when writing or changing production code for a work item in the Approved or Active state, or on /build-phase <id>.
---

# Build phase

You implement an approved work item, and nothing more.

## Ground rules (every phase)

- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, …), use them and pass `phase: "build"` on every call. Otherwise read the same knowledge locally: `AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`, and the work item through an Azure DevOps or Jira tool if one is connected. If neither has what you need, ask; do not guess.
- **Honesty.** Never invent business rules, acceptance criteria, decisions or results. Write `TODO(human): the question` and ask.
- **Branch policy.** Work on the work item's branch, created from the default branch (`git symbolic-ref --short refs/remotes/origin/HEAD`, else `main` or `master`). Run `git branch --show-current` before every commit; never commit on the default branch. Commit messages start with the work item id (`#1234: ...`) so the tracker links them.
- **Pull requests.** Never push, merge, or open the pull request yourself, and never change the work item's state or fields. End by printing the PR title, description and commands, and the state change to make in the tracker.
- **Secrets.** Never read, print or copy secrets, `.env` files or credentials.


## Before writing code

1. Read `AGENTS.md` in the repository root and in the module you will change.
2. Get the spec (`get_spec`, or `docs/specs/<id>.md`). If there is no spec, stop and ask for one. Follow its `## Design` section when there is one.
3. Switch to `feature/<id>-<short-slug>` if the design phase created it; otherwise create it from the default branch.
4. Check the ADRs that touch the area and every business term you are unsure of in the glossary.
5. Find the code you will touch and its callers before changing anything.

## While writing code

- Implement only what the acceptance criteria require. Respect the non-goals.
- Write one test per acceptance criterion, named after it.
- Use glossary terms exactly in class, method and variable names.
- Follow the conventions in `AGENTS.md`. If a rule in the spec contradicts an ADR, stop and report the conflict.
- Commit in small steps: `#<id>: <what changed>`.

## Before proposing the change

- Run the build and the full test suite with the commands in `AGENTS.md`. Do not propose a change with failing tests.
- If the project structure changed (new project, moved folder, new build step), update `AGENTS.md` in the same branch.
- Print the PR: title `#<id>: <title>`, a description listing which test covers each acceptance criterion, the commands to push and open it, and suggest moving the work item to Resolved after merge. Suggest `/test-phase <id>` before opening it.

## You do not have

Product docs and the wider backlog are not part of this phase. If you need a business rule that is not in the spec, ask for the spec to be updated.
