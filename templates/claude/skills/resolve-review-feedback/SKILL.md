---
name: resolve-review-feedback
description: Apply fixes for existing code-review feedback on a pull/merge request — not writing a new review, but actually implementing what the reviewers (human or automated) already flagged. Use this whenever the user says things like "resolve the comments on this PR", "fix what the review found", "address the review feedback", "implement my review notes", "can you take care of the things I flagged on PR #X", or hands over a PR/MR that already has review threads and wants the underlying code fixed. Works across GitHub, Azure DevOps, GitLab, or any repo the user has tool access to. Push this to trigger even if the user doesn't say the word "review" explicitly — "clean up PR 55" or "make the changes Hery asked for" also qualify if there's an existing review to work from.
---

# Resolve Review Feedback

Turn review comments that already exist on a PR into an actual, correct code change — not a re-review, and not a generic "apply best practices" pass. The two failure modes to avoid are (1) fixing the wrong thing because you trusted a stale local checkout or a mis-stated review comment, and (2) fixing it in a way that's technically defensible but ignores how *this* codebase actually does things.

Treat this as a research task first and a coding task second. The review comments tell you *where to look*; they are not automatically correct about *what's wrong* or *how to fix it*.

## 1. Establish ground truth before writing any fix

Pull the PR's description, every review thread (not just the top-level summary comment — the inline threads often carry the precise fix), and the diff. Then, critically: fetch the **exact current file content from the platform** (e.g. "get file at this branch/ref") rather than assuming a local checkout matches. Local working directories drift — they may be on a different branch, mid-refactor, or simply stale. If you find a mismatch between what a review comment claims and what the code actually does, the code wins; note the discrepancy rather than silently propagating a wrong claim into your fix or your summary.

This applies even to review comments *you* wrote earlier in the same session. A prior automated pass can be wrong — e.g. it might claim "the rest of the codebase uses convention X, so this file is inconsistent" without having actually verified that claim against the other files. Before using such a claim to justify a change, go check the files it's citing. If the claim doesn't hold up, say so and don't make the change, or make a narrower one.

## 2. Read the project's own rules before drafting fixes

Before touching code, look for the project's own style/contribution guide (CLAUDE.md, CONTRIBUTING.md, a linter config, an existing similar file) and mirror its actual patterns — naming conventions, error-handling shape, where shared logic like validation schemas or hooks live, theming/design-token usage, i18n conventions, etc. A fix that's "best practice" in the abstract but doesn't match the surrounding code will just create a new inconsistency. When you need a template for how something should look (e.g. "how do mutation hooks get named here", "how are Zod schemas organized"), find an existing correct instance in the same repo and match it exactly, rather than inventing your own convention.

## 3. Fix file by file, scoped tightly to the finding

For each flagged issue: understand *why* it's wrong (not just "the reviewer said so" — trace the actual bug or violation), then make the smallest correct change that fixes it, preserving everything else in the file. Resist the urge to also refactor adjacent code that wasn't flagged — that inflates the diff and the risk of the fix. If a fix legitimately requires touching an unrelated file (e.g. renaming a shared hook forces updates in every import site), that's fine — just keep the reason traceable back to a specific finding.

If a review comment turns out to describe something that would require a large architectural change (swapping an entire HTTP client layer, rewriting a data model, migrating a build system) and the codebase isn't actually ready for it (check: does the target of the migration even exist yet, e.g. is there a generated client already, is there a config already wired up), don't force a rushed version of it into this fix pass. Instead do what the review itself usually implies is the fallback: track it explicitly as a follow-up (a ticket/issue in the project's tracker if you have tool access to create one) and leave a clear comment/reference near the code. Say plainly in your summary that this was deferred and why.

## 4. Commit safely — don't clobber other work

Before touching the working directory, check whether it's currently on the branch you need, and whether it has *other* uncommitted or in-progress work that belongs to a different task. If so, don't `checkout`/`reset`/`stash` your way past it — that risks destroying work that has nothing to do with this fix. Prefer an isolated path:

- A separate `git worktree` checked out to the target branch, so the user's current checkout is never touched.
- If the environment is too unstable for a full worktree checkout (flaky mounted filesystems, sandboxed VMs can behave this way), fall back to low-level git plumbing scoped only to the files you're changing: read the target branch's tree into a scratch index (`GIT_INDEX_FILE` pointed somewhere temporary, `git read-tree <branch>`), write your fixed files and `git hash-object -w` + `git update-index --cacheinfo` each one individually rather than in one big batch (bulk operations are where instability tends to show up — one file per operation is more resilient and easier to recover from if something crashes partway), then `git write-tree` + `git commit-tree` with the branch's current tip as parent, and finally update the branch ref. Diff the resulting tree against the original branch tree before committing (`git diff-tree --name-status`, not a full content diff if the environment is fragile) as a final sanity check that only the intended files changed.
- Whatever path you take, verify at the end that the branch/checkout the user was already working on is untouched.

## 5. Be honest about what you could and couldn't do

If you can't push (no network access to the remote, no write credential, sandboxed environment), don't claim the fix is live on the PR. Say exactly what's true: the commit exists locally on branch X at commit Y, and here is the exact command the user needs to run themselves to publish it. Same principle for anything else you couldn't verify — if you didn't run the test suite or linter because the environment couldn't support it, say so plainly rather than asserting things pass.

## 6. Report back mapped to the original findings, not as a generic changelog

Structure the summary around the review findings themselves (blocking/major first, then minor/naming), so the user can check each one off against what they read in the review. Explicitly call out:
- What was fixed, and briefly how.
- What was deliberately deferred, and why (with a link to any ticket you filed).
- Any corrections — places where a review claim didn't hold up once you checked the actual code, and what you did instead.

Don't just say "resolved all comments" — the value here is precision, not a clean-sounding summary.
