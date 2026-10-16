---
name: requirements-phase
description: Turn a work item into a committed spec with testable acceptance criteria, ready for review. Use for a work item in the New state with no spec yet, or on /requirements-phase <id>.
---

# Requirements phase

You turn one work item into a spec that a human reviews before anyone designs or builds. You describe **what** must happen, never **how**.

## Ground rules (every phase)

- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, …), use them and pass `phase: "requirements"` on every call. Otherwise read the same knowledge locally: `AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`, and the work item through an Azure DevOps or Jira tool if one is connected. If neither has what you need, ask; do not guess.
- **Honesty.** Never invent business rules, acceptance criteria, decisions or results. Write `TODO(human): the question` and ask.
- **Branch policy.** Work on the work item's branch, created from the default branch (`git symbolic-ref --short refs/remotes/origin/HEAD`, else `main` or `master`). Run `git branch --show-current` before every commit; never commit on the default branch. Commit messages start with the work item id (`#1234: ...`) so the tracker links them.
- **Pull requests.** Never push, merge, or open the pull request yourself, and never change the work item's state or fields. End by printing the PR title, description and commands, and the state change to make in the tracker.
- **Secrets.** Never read, print or copy secrets, `.env` files or credentials.


## Steps

1. Read the work item (`get_work_item` or the tracker tool). Note its id, title, description, acceptance criteria and comments.
2. Create the branch `feature/<id>-spec` from the default branch (or switch to it if it exists).
3. Find the business rules that apply (`search_product_docs`, or the `ai-ready` wiki pages you can read) and check every business term in the glossary.
4. Write `docs/specs/<id>.md` from `docs/specs/TEMPLATE.md`, keeping its headings: Goal, Acceptance criteria, Edge cases, Non-goals, Business rules involved, Related ADRs.
   - Acceptance criteria as numbered *Given / When / Then* statements, each one testable.
   - Edge cases: empty input, concurrency, timeouts, permissions, limits.
   - Explicit non-goals, so later phases do not expand the scope.
   - Risk tier: High for money, personal data, security or irreversible operations.
5. Add every question for the product owner under `Open questions` instead of guessing.
6. If `scripts/check-docs-templates.sh` exists, run it. Commit: `#<id>: spec`.

## Done when

The spec has testable acceptance criteria, non-goals and a risk tier, and every unknown is an open question. Print the PR (`#<id>: spec for <title>`, target the default branch) and suggest moving the work item to Approved once the spec PR is merged.

## You do not have

The code, the code graph and the ADRs are not part of this phase. If you catch yourself describing classes or tables, stop: that is the design phase.
