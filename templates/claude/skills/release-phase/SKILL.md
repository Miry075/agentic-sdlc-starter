---
name: release-phase
description: Prepare a release from merged work items: release notes, a checklist of migrations, configuration and feature flags, and a rollback plan. Use when preparing a release or on /release-phase <version>.
---

# Release phase

You prepare the release. You never deploy, tag, publish or change environments.

## Ground rules (every phase)

- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, …), use them and pass `phase: "release"` on every call. Otherwise read the same knowledge locally: `AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`, and the work item through an Azure DevOps or Jira tool if one is connected. If neither has what you need, ask; do not guess.
- **Honesty.** Never invent business rules, acceptance criteria, decisions or results. Write `TODO(human): the question` and ask.
- **Branch policy.** Work on the work item's branch, created from the default branch (`git symbolic-ref --short refs/remotes/origin/HEAD`, else `main` or `master`). Run `git branch --show-current` before every commit; never commit on the default branch. Commit messages start with the work item id (`#1234: ...`) so the tracker links them.
- **Pull requests.** Never push, merge, or open the pull request yourself, and never change the work item's state or fields. End by printing the PR title, description and commands, and the state change to make in the tracker.
- **Secrets.** Never read, print or copy secrets, `.env` files or credentials.


## Steps

1. Find what is in the release: commits on the default branch since the last tag (`git describe --tags --abbrev=0`, then `git log --oneline <tag>..HEAD`), and the work item ids in their messages.
2. For each work item, read its spec and verification section. List any item without a passing verification as a blocker.
3. Create the branch `feature/release-<version>` from the default branch.
4. Write `docs/releases/<version>.md` with:
   - **Release notes** for users, per work item, in plain language.
   - **Checklist:** database migrations and their order, configuration and secrets to add (names only, never values), feature flags, infrastructure changes, and the ADRs that changed.
   - **Rollback plan:** how to undo each risky change, and which ones cannot be undone.
   - **Blockers:** from step 2.
5. Commit: `release <version>: notes and checklist`.

## Done when

Every work item in the release is listed with its verification status, every migration and configuration change is in the checklist, and the rollback plan covers every High-risk item. Print the PR and say clearly whether there are blockers.

## You do not have

The code graph is not part of this phase. Work from the specs, verifications and commit history.
