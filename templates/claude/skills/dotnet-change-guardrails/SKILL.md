---
name: "dotnet-change-guardrails"
description: "Standing engineering guardrails for taking a code change from branch creation through to commit in a .NET repository — working-tree and branch safety before git operations, grounding analysis in actual code, scope discipline, build/test verification before any assessment, a health-sector/PHI sweep, and staging discipline at commit time. Use this whenever implementing, reviewing, verifying, or committing a code change in a .NET repo, and always alongside the azure-workitem-flow skill (each of its gates maps to a phase here). Also use when the user asks what to check before committing, whether a change is safe, or what could break."
---

# .NET change guardrails

Standing guardrails for moving a code change from branch to commit. Organized by phase so a driving workflow can point at the phase it is currently in — apply the phase you are in rather than reciting the whole document.

The through-line: never report something as done or safe that you have not actually observed. Most of the expensive failures in this workflow come from plausible-sounding claims, not from bad code.

## Phase A — Before touching git

**Guard the working tree.** Check it is clean before any `checkout` or `pull`:

```
git status --porcelain
```

Empty output means clean — proceed. Anything else: stop, show the user what is uncommitted, and ask how to handle it (commit, stash, or discard). Checking out over a dirty tree either fails or carries edits into the wrong branch, and pulling on top of local changes can raise conflicts the user never asked for. The check costs a second; the recovery costs much more.

**Check the branch does not already exist** before creating it:

```
git rev-parse --verify --quiet <branch-name>
```

If it exists, ask whether to reuse it or choose another name rather than hitting an error halfway through.

**Branch prefix is always `feature/`.** Regardless of work item type — Bug, Task, or User Story — the branch name starts with `feature/<work-item-id>-<slugified-title>`. Don't derive the prefix from type.

## Phase B — Analysis

**Ground the analysis in the code, not the ticket text.** Cite concrete locations (`Services/ClaimSubmissionService.cs:142`) for the code the change will touch. An analysis reasoned purely from ticket prose reads convincingly and is often wrong about where the change actually lands; requiring citations makes it visible whether the codebase was really read.

**Convert acceptance criteria into a verifiable checklist.** Restate each AC as something that can be checked against running code, and carry that list forward to Phase D. This is what prevents delivering something adjacent to what was asked. If the AC are missing or vague, say so and state the assumption you are proceeding on rather than quietly inventing requirements.

**Do not manufacture options.** When a request has one sensible implementation, say so and note the alternatives you discarded in a line each. Padding to a quota of three with straw options wastes the reviewer's time and hides the real decision.

## Phase C — Implementation

**Hold scope.** Implement the chosen approach and nothing else — no opportunistic renames, reformatting, package bumps, or drive-by refactors in surrounding code. Collect anything you notice and raise it as follow-up in Phase D. A diff that contains only the intended change is reviewable; one padded with unrelated edits is not, and it makes the commit dishonest about what happened.

**Follow local convention over general best practice.** Before introducing a pattern, find a nearby example in the repo: DI registration style, async/`CancellationToken` usage, logging abstraction, error-handling approach, nullable reference context, validation approach, and the test framework actually in use. Consistency with the codebase matters more than importing habits from elsewhere.

## Phase D — Verification (before presenting any assessment)

**Run it, do not claim it:**

```
dotnet build --nologo
dotnet test --nologo
```

Report the real outcome, including failures. If the environment cannot run these (no SDK, no access to private NuGet feeds, no test host), say so plainly and label the assessment **unverified — not built**, so the user knows exactly what they are receiving. An assessment presented as fact but derived only from reading code is the single most costly failure mode in this workflow.

**Walk the AC checklist from Phase B item by item**, marking each met, not met, or not verifiable.

### Health-sector sweep

For clinical or otherwise regulated codebases, check each of these every time and report the result — including when the answer is "checked, nothing found". Silence is indistinguishable from not having looked:

- Patient or PHI data reaching logs, traces, exception messages, or telemetry.
- Audit trail: is an action that must be auditable now happening without a record?
- Retention or deletion behavior changed.
- Data crossing a region or tenant boundary (new outbound call, new storage target).
- AuthN/AuthZ: does a new endpoint, handler, or job inherit the intended policy, or does it default to open?

**Then state the risks honestly**, including the parts you are unsure about. "I could not verify X" is more useful than a confident guess.

## Phase E — Commit

**Stage named files only** — never `git add -A` or `git add .`, which sweeps in unrelated local files. Show `git status` and `git diff --stat` and get approval for exactly what is staged.

**Message format:** `<type>: <short summary> (AB#<work-item-id>)`, imperative mood. Add a body explaining *why* when the reason is not obvious from the diff.

**One commit** unless the change has genuinely separable parts, in which case make each one independently sensible.

## Cross-cutting — resilience across turns and sessions

This work often spans days, and conversation context may be gone when it resumes. Re-derive state from the repository instead of assuming memory:

```
git rev-parse --abbrev-ref HEAD      # current branch — carries the work item ID
git status                            # uncommitted work in progress
git diff                              # what has changed but is not staged
git log <base-branch>..HEAD --oneline # what is already committed on this branch
```

When resuming, state what you inferred from the repo before acting on it, so the user can correct a wrong inference cheaply.