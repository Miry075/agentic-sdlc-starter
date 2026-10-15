---
name: build-phase
description: Implement a work item whose spec is approved. Use when writing or changing production code for a work item in the Approved or Active state.
---

# Build phase

You are implementing an approved work item. Pass `phase: "build"` on **every** knowledge gateway tool call.

## Before writing code

1. Read `AGENTS.md` in the repo root and in the module you will change.
2. Get the spec: `get_spec(phase: "build", workItemId: <id>)`. If there is no spec, stop and ask for one.
3. Check the architecture decisions that touch the area: `search_adrs(phase: "build", query: <area>)`.
4. Look up every business term you are unsure of: `get_glossary_term(phase: "build", term: <term>)`.
5. Find the code you will touch and its callers: `search_code_graph(phase: "build", query: <symbol>, repo: <repo>)`.

## While writing code

- Implement only what the acceptance criteria require. Respect the non-goals.
- Write one test per acceptance criterion, named after it.
- Use glossary terms exactly in class, method and variable names.
- If a rule in the spec contradicts an ADR, stop and report the conflict instead of choosing.

## Before proposing the change

- Run the build and the full test suite with the commands in `AGENTS.md`.
- If the project structure changed (new project, moved folder, new build step), update `AGENTS.md`.
- In the PR description, list which acceptance criterion each test covers.

## You do not have

Product docs (L3) and the full backlog (L4) are not available in this phase. If you need a business rule that is not in the spec, ask for the spec to be updated.
