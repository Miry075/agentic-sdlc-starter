---
name: test-phase
description: Verify a built work item against its spec: every acceptance criterion has a passing test, edge cases are covered, results are recorded. Use before opening the build PR, for a Resolved work item, or on /test-phase <id>.
---

# Test phase

You check that the change does what the spec says, and only that. You add tests; you do not change production code.

## Ground rules (every phase)

- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, …), use them and pass `phase: "test"` on every call. Otherwise read the same knowledge locally: `AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`, and the work item through an Azure DevOps or Jira tool if one is connected. If neither has what you need, ask; do not guess.
- **Honesty.** Never invent business rules, acceptance criteria, decisions or results. Write `TODO(human): the question` and ask.
- **Branch policy.** Work on the work item's branch, created from the default branch (`git symbolic-ref --short refs/remotes/origin/HEAD`, else `main` or `master`). Run `git branch --show-current` before every commit; never commit on the default branch. Commit messages start with the work item id (`#1234: ...`) so the tracker links them.
- **Pull requests.** Never push, merge, or open the pull request yourself, and never change the work item's state or fields. End by printing the PR title, description and commands, and the state change to make in the tracker.
- **Secrets.** Never read, print or copy secrets, `.env` files or credentials.


## Steps

1. Switch to the work item's branch `feature/<id>-<short-slug>`. Read the spec `docs/specs/<id>.md`.
2. Build a table: each acceptance criterion and edge case, the test that covers it (file and test name), and whether it exists.
3. Write the missing tests. Name each after its criterion. Use the test framework and conventions already in the repository.
4. Run the full test suite with the command in `AGENTS.md`.
5. If a test fails because the production code is wrong, do **not** fix the code: record the failure, the criterion it breaks and how to reproduce it, and send it back to the build phase.
6. Add a `## Verification` section to the spec: the table from step 2 with results, the command you ran, and the date.
7. Commit: `#<id>: tests and verification`.

## Done when

Every acceptance criterion and edge case has a test, the suite passes, and the verification table is in the spec. Otherwise, the failures are listed for the build phase. Print the PR description section with the verification table.

## You do not have

Product docs and the backlog are not part of this phase. Test against the spec, not against what you think the feature should do.
