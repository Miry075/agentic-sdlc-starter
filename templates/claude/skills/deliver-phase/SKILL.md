---
name: deliver-phase
description: Send the work item for review and move it to dev-done (azure-workitem-flow step 8) - ask who opens the pull request, open it only with approval, then propose Completed (Hours) as a human-equivalent estimate and the Resolved transition, never Closed. Handles review comments with resolve-review-feedback. Use after /test-phase, or on /deliver-phase <id>.
---

# Deliver phase: pull request and resolve

You get the committed work in front of reviewers and record it in the tracker, each write only after the user approves it.

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
- **Knowledge.** If the knowledge gateway tools are available (`get_work_item`, `get_spec`, `get_glossary_term`, `search_adrs`, ...), use them with `phase: "deliver"` on every call. Otherwise read the same knowledge locally (`AGENTS.md`, `docs/specs/<id>.md`, `docs/domain/glossary.md`, `docs/adr/`) and the work item through the Azure DevOps MCP tools. Ground every claim in the code with `file:line` citations.
- **Honesty.** Never invent requirements, decisions or results. Never report as done, built or passing what you have not observed; label it unverified.
- **Commits:** named files only (never `git add -A` or `git add .`), `git status` and `git diff --stat` shown for approval first, message `<type>: <short summary> (AB#<work-item-id>)`, tests in the same commit as the code they cover.
- **Remote and tracker writes only with explicit approval for that action:** pushing, opening the pull request, setting hours or changing the work item state. Never merge, never vote on your own pull request, never move a work item to Closed.
- **Interactive only.** This flow needs the user at each gate. In an unattended run, stop and say so.
- **Secrets and personal data:** never read, print or copy secrets, `.env` files, credentials, or patient or personal data.


## Step 8a: the pull request

1. **Ask who opens the pull request**, every time, with no standing assumption: you, or the user.
2. If you do it, and only after that answer: push the branch (`git push -u origin feature/<id>-...`) and open the pull request against the base branch from the requirements phase, with the title carrying the work item id, a description summarising the change (the verification table from the spec, the risks, the follow-ups) and `AB#<id>`, and the work item linked (`az repos pr create ... --work-items <id>`, or the MCP pull request tool). If you cannot push (no network or credentials), give the exact commands instead and say the change is only local.
3. If the user does it, wait until they say the pull request exists.

## Step 8b: completed hours and the dev-done state

Once the pull request exists, propose **both** writes in one prompt and wait:

1. **Completed (Hours)** (`Microsoft.VSTS.Scheduling.CompletedWork`), set first because many templates block the transition while it is empty. Estimate what the change would have cost a **senior developer working by hand**: reading the item and code, implementing, writing tests, self-review. Not your wall-clock time, not padded. Show a one-line breakdown. The field is cumulative: propose the addition and the new total. Set `RemainingWork` to 0 if the template has it. If the user gives a number, take theirs.
2. **The transition** to the item type's dev-done state (`Resolved`, `Done` or `Code Review`; read the allowed transitions, do not assume), with an accurate `Reason` if offered.

**Never move the item to Closed.** A pull request under review is not verified work. Report what you wrote (state, reason, hours, new total) and the pull request URL.

## Review comments

When reviewers comment, follow `resolve-review-feedback`: fetch every thread and the current file content from the platform (not a stale local copy), check each claim against the code, follow the project's own conventions, fix file by file in the smallest correct change, defer architectural asks as tracked follow-ups, never clobber other work in the checkout, and report back finding by finding, including corrections where a comment did not hold up. Commits follow the same rules, on the same branch.

## Done when

The pull request exists, the item is in its dev-done state with hours recorded, and no review thread is left unanswered. Merging and closing belong to the reviewers.
