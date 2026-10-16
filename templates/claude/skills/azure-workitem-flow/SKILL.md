---
name: "azure-workitem-flow"
description: "Guides an Azure DevOps work item from a pasted link through to a reviewed, committed code change and a resolved ticket. Use whenever a message contains a dev.azure.com work item URL (a path containing _workitems/edit), or the user says let's start on this work item, pick up this task, or implement this user story. Fetches the item, creates the branch, lists the existing acceptance criteria as a confirmed baseline before any new work, runs an Opus-powered what-and-why analysis, sweeps the use cases, proposes 2-3 options assessed on design quality and testability, takes the implementation go-ahead, reviews risk, commits, then on pull request proposes the Resolved transition with a human-equivalent Completed Hours estimate — pausing for the user's decision at every step and applying dotnet-change-guardrails, dotnet-design-principles and dotnet-testing-standards throughout. Do not skip straight to coding when a work item link appears; always run this workflow first."
---

# Azure DevOps work item → branch → analysis → implementation → commit → PR → resolved

## Trigger

A message containing a dev.azure.com work item URL, e.g. a path of the form `_workitems/edit/<id>`. Extract the organization, project, and work item ID from the URL — org and project are needed for the API calls below, and the ID for the branch name and commit message.

## Before you start

**Load the guardrails.** Invoke the `dotnet-change-guardrails` skill at the start of this workflow. Depending on how it is installed it may appear plainly as `dotnet-change-guardrails`, namespaced (`anthropic-skills:dotnet-change-guardrails`), or grouped under `claude.ai sync` — all the same skill. If it is not available as a skill, read `../dotnet-change-guardrails/SKILL.md` relative to this file. It carries the engineering standards for each step below — git safety checks, verification requirements, the health-sector sweep, commit hygiene — and this workflow assumes they are in force. Each step names the guardrail phase that applies to it; do not restate that content here, apply it.

**Load the design principles.** Also invoke `dotnet-design-principles` (same naming caveats as above; fall back to reading `../dotnet-design-principles/SKILL.md`). Where `dotnet-change-guardrails` governs process safety, this one governs whether the code itself is well-structured — SOLID, DDD, layering. It applies at step 4 (choosing the approach), step 5 (implementation) and step 6 (review) below.

**Load the testing standards.** Also invoke `dotnet-testing-standards` (same caveats; fall back to `../dotnet-testing-standards/SKILL.md`). It decides what tests must exist, and applies from step 4 onward — testability is an input to the design choice, not an afterthought.

Also, before the first step:

- Git operations run in the repository the session is working in. If that is ambiguous (no repo in the working directory), ask which repo before touching git.
- Stay direct and technical — no filler, no reassurance you do not mean.

## Fetch the work item

This is preparatory — it does not need the user's input unless it fails. Use the `azure-devops` MCP server (Microsoft's official `azure-devops-mcp`). Its tool naming has changed across versions and is still described in its own docs as "being aligned" with a newer structure, so check the actual tool list once (`/mcp` or the tool picker) rather than guessing blind, and use whichever of these two shapes is actually present:

- **Current (consolidated) shape:** one `wit_work_item` tool with an `action` parameter — `action: "get"` (params: `id`, `project`, `expand: "All"`) for fields plus relations, `action: "list_comments"` (params: `workItemId`, `project`) for the discussion thread, `action: "get_batch"` (params: `ids`, `project`) for linked items.
- **Older (per-operation) shape:** separate tools `wit_get_work_item` (`id`, `project`, `expand: "all"`), `wit_list_work_item_comments` (`workItemId` or `id`, `project`), `wit_get_work_items_batch_by_ids` (`ids`, `project`).

1. Get the work item itself with whichever shape is present, expanding relations — this returns the fields (title, description, acceptance criteria, type, state, tags, area/iteration path, effort/scheduling fields) and links to parent/child/related items in one call.
2. List its comments — the real requirement sometimes lives in a comment, not the description.
3. If step 1 surfaced parent, child, or related work item IDs, batch-fetch them for title/type/state context. One level is enough unless the user asks to go deeper.
4. If any of these calls fail (auth, wrong project, tool/action not found), say so plainly with the actual error rather than silently retrying the other shape forever, and fall back to asking the user to paste the title, description, and acceptance criteria so the workflow can still proceed.

**Collect acceptance criteria from everywhere they hide, and separate them into two sets.** Many of these items extend an implementation that already exists, and the work item then carries both the original criteria and criteria appended later. Read the `Acceptance Criteria` field, the description, any appended or "additional AC" section, and the comments — appended criteria very often arrive as a comment rather than an edit to the field. Note any `Security Criteria` field too; in regulated projects (health, finance) it is not decoration.

- **Existing AC** — criteria describing behaviour that is already implemented. These are the regression baseline: the change must not break them, and they are not work to redo.
- **Additional AC** — criteria new to this change. These are the actual delta to build.

If which set a criterion belongs to is unclear — no timestamps, no explicit "additional" heading — say what you inferred and from what, and have the user confirm before step 4. Getting this split wrong means either rebuilding working behaviour or silently regressing it.

Carry everything fetched here into the analysis step below.

## The process, step by step

Never skip a step, and never advance until the user has actually responded to the current one. These pauses exist so the user reviews the analysis, chooses the approach, and approves the code before anything is committed.

### 1. Create the branch

Apply guardrails **Phase A** (working-tree check, branch-exists check) before running any git command.

1. Ask which branch to branch from (e.g. `origin/develop`, `origin/main`).
2. Update that base branch locally — checkout and pull. Only pull; no rebase, no cross-branch merge, nothing else:
   ```
   git checkout <base-branch>
   git pull
   ```
   If the pull fails (no network reach to the remote from this sandbox), say so plainly, hand over the exact commands to run locally, and continue on the assumption the user has updated it.
3. Propose a branch name: `feature/<work-item-id>-<slugified-title>` (lowercase, hyphens, ~40 chars) — always the `feature/` prefix, regardless of work item type. Let the user adjust it.
4. Once confirmed, create it from the updated base branch:
   ```
   git checkout -b <branch-name>
   ```

### 2. Analyze the work item — what & why

Apply guardrails **Phase B** — file:line citations, acceptance criteria converted into a verifiable checklist.

Spawn a subagent via the Agent tool with `model: "opus"`. The user specifically wants Opus for this pass regardless of which model runs the main session. Give it everything fetched above — fields, comments, both AC sets, and any linked-item context — and either the relevant repo context or instructions to explore the repo itself, so the analysis is grounded in the codebase rather than the ticket prose. Ask it to report, **in this order**:

- **Existing AC checklist — first, always.** The already-implemented criteria, each as a verifiable item, with the code that satisfies it today (`file:line`). This is the regression baseline for step 6. If a criterion claims to be implemented and the code does not actually satisfy it, say so — that is a finding, not a checklist entry. If the work item genuinely has no prior criteria (a net-new implementation), state that in one line rather than omitting the section.
- **What** — a precise restatement in engineering terms, with the code locations the change will touch.
- **Why** — the underlying business or technical motivation.
- **Additional AC checklist** — the new criteria, each as a verifiable item. This is the work.

**Gate: the existing AC are listed and confirmed before any new work is proposed.** Present that list explicitly — as a list, not folded into prose — and stop. Do not enter step 3, propose options, or touch code until the user has confirmed the baseline is complete and correct. They know what the system already does better than the ticket does. Everything after the existing-AC list may be relayed as prose rather than a wall of bullets.

### 3. Identify the use cases

Apply guardrails **Phase B** discipline here too — ground this in the actual code and the AC checklists from step 2, don't invent scenarios in the abstract.

Before proposing how to solve it, enumerate every use case the change has to hold up under — not just the happy path implied by the ticket:

- Each actor or caller that exercises this code path (different user roles, an API caller vs. a UI caller, an automated job vs. an interactive user).
- Edge cases and boundary conditions the code will actually see (empty or null input, concurrent access, large volumes, an already-failed or already-completed state).
- Existing behavior elsewhere in the system that depends on what you're about to touch — anything that would regress if you're not careful. The confirmed existing AC list is the first source for this, not an afterthought.
- Related workflows that share this code path but aren't the one named in the ticket.

Write this as a short checklist, the same way the AC became one in step 2. Step 4 evaluates every solution option against this checklist, and step 6 checks the finished implementation against it — so keep each item concrete enough to verify later, not a vague "handle edge cases" placeholder.

If a use case turns out to be out of scope for this change, say so explicitly rather than silently dropping it, so the user sees you considered it and chose not to cover it, not wonder whether you missed it.

### 4. Propose solution options

Apply guardrails **Phase B** on option honesty: 2-3 realistic options when they genuinely exist, one clearly-argued option when they do not — never padded with straw alternatives.

For each option, give:

- What it involves.
- Its principal trade-off (complexity, risk, performance, maintainability, effort).
- How it holds up against the use-case checklist from step 3, and whether it preserves the existing AC — say plainly if an option fails to cover one rather than glossing over it.
- **Design fit** — how it sits against `dotnet-design-principles`: which SOLID principle or DDD boundary it respects or strains, where the logic lands in the layering, whether it extends existing code or duplicates it. An option that only works by putting domain logic in a service, or by having Domain reach into Infrastructure, is a worse option and must be described as one.
- **Testability** — what tests it would need per `dotnet-testing-standards`, and whether they can be written without contorting the design. An option that is cheap to build but needs heavy mocking or new seams to test is more expensive than it looks; say so here, before it is chosen.

Close with your own recommendation: the option that best respects every use case, the existing AC, and the design and testing standards — not just the one with the best trade-off in isolation — and a one-line reason for the choice. Prefer the simplest option that meets those bars; reusable and sustainable means fitting the existing structure well, not adding structure. Then stop and wait for the user's choice — AskUserQuestion works well here. Do not proceed until they have picked.

### 5. Get the implementation go-ahead

Ask the user to confirm you should implement the chosen option before writing any code. Once confirmed, implement it directly in the current session — implementation is meant to run on Sonnet, and that's already the model in play, so no subagent handoff is needed.

Apply guardrails **Phase C** while implementing: hold scope to the chosen option, follow local repo conventions, and park anything else you notice as follow-up. Apply `dotnet-design-principles` to the code you write — SOLID, DDD, layering, in the code itself, regardless of what surrounds it — without manufacturing structural changes the ticket didn't ask for; see that skill's minimal-footprint section.

Write the tests as part of this step, not after it, per `dotnet-testing-standards`: tests for the additional AC and the use cases they map to, and a test pinning any existing AC this change puts at risk. For a bug fix, the failing test comes first.

### 6. Review the implementation

Apply guardrails **Phase D** first — build and test for real, walk both AC checklists, run the health-sector sweep. Also walk the use-case checklist from step 3, item by item, the same way: met, not met, or not verifiable. Do not present an assessment before that, and if the environment cannot build or test, label the assessment unverified and say why.

Then give your own honest read on:

- **Existing AC** — each baseline criterion from step 2, still holding or not. Report this first; a met delta on top of a regression is not a success. Name anything you could not verify rather than assuming it survived.
- **Additional AC** — each new criterion, met or not, and the test that proves it.
- What you actually did, and how it maps to the chosen option.
- What still needs to happen (tests, config or migration steps, documentation, follow-up work items).
- Side effects and risks — breaking changes, performance, security, backward compatibility, and anything you could not verify.
- Which `dotnet-design-principles` were actually relevant to this change and how the code satisfies them, plus any pre-existing violations nearby you flagged rather than fixed or copied.
- Test coverage per `dotnet-testing-standards` — what you tested, at which layer, and anything intentionally left untested with the reason.

Do this even for a one-line change; small diffs still carry side effects worth naming. If one of these areas is genuinely empty, say so in a line rather than dropping it.

### 7. Commit the change

Only after the review step above, ask the user to confirm everything is good to commit. Apply guardrails **Phase E** — named files only, `git status` and `git diff --stat` shown for approval, then:

```
git add <changed files>
git commit -m "<type>: <short summary> (AB#<work-item-id>)"
```

`AB#<id>` is Azure DevOps's convention for auto-linking a commit to its work item — always include it. Tests belong in this same commit as the code they cover. Stop here — push and pull request belong to step 8, never to this step.

### 8. Pull request, then resolve with completed hours

**Ask who opens the PR — every item, no standing assumption.** Either you push the branch and open the pull request against the base branch from step 1 (title carrying the work item ID, description summarising the change and linking `AB#<id>`), or the user does it and tells you when it exists. Push nothing and open nothing until they have answered; a push is not covered by the commit approval in step 7.

Once the PR exists, the work item moves to **dev-done, not verified**. Propose both writes in one prompt, and wait:

1. **Completed (Hours)** — `Microsoft.VSTS.Scheduling.CompletedWork`. Many templates block the resolve transition while this is empty, so it is set first. Estimate what this change would have cost **a senior developer working by hand**: reading the item and the surrounding code, implementing, writing the tests, self-review. It is not your wall-clock time, and it is not padded to look substantial. Show the estimate as a one-line breakdown so the user can correct it — the value lands in sprint capacity and reporting, so a wrong figure distorts the team's numbers, not just this ticket. The field is cumulative: if it already holds hours, propose the addition and state the resulting total rather than overwriting. If the template also has `RemainingWork`, propose setting it to 0 in the same write. If the user would rather just give a number, take theirs without arguing.
2. **The transition** — to `Resolved`, or whatever the type's allowed transitions call the dev-done state (`Resolved`, `Done`, `Code Review`, depending on the process template — read them, don't assume). Set an accurate `Reason` if the template offers one.

**Never move the item to `Closed`.** A PR under review is not verified work; closing it is the reviewer's or tester's call, not yours. Say so if the user asks you to close it and they can then tell you to do it anyway.

Report what you wrote — state, reason, hours, new total — and the PR URL. No merge, no vote on your own PR.

## Notes

- If new information surfaces at any step (a related work item, a constraint that was not visible), loop back and revise rather than forcing forward progress.
- Resuming later in a new session: apply the guardrails' cross-cutting section — re-derive the branch, work item ID, and progress from git, and state what you inferred before acting on it.
- These steps serve the user's judgment, not your convenience. When unsure whether the user has actually responded to a step, ask rather than assume.