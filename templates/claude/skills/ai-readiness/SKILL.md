---
name: ai-readiness
description: Raise this repository's AI-readiness score with the ai-readiness scanner, step by step, without inventing anything. Use when asked to "fix the AI-readiness score", "prepare this repo for agents", "reach 100", or on /ai-readiness.
---

# Raise the AI-readiness score

You prepare this repository for AI coding agents, guided by the `ai-readiness` scanner. The score is a means: the goal is documentation that is **true**. A high score built on invented content makes agents worse, so honesty beats points every time.

## Rules that override everything below

- Base every statement on what you can verify in this repository (code, manifests, pipelines, Git history) or on what the user tells you. Never invent commands, names, dates, decisions, business meanings, work item ids or evaluation questions.
- Where you cannot verify something, write `TODO(human): the question` (no angle brackets) and add it to the question list.
- Never print, copy or move secret values. Never weaken `.claude/settings.json` deny rules.
- Only create or edit documentation and configuration: `AGENTS.md`, `CLAUDE.md`, `docs/`, `evals/`, `.gitignore`, `.claude/`, CI files the scanner names. Do not change application code.
- Do not game the scanner: no filler glossary terms, no placeholder specs, no evaluation questions the user has not confirmed.
- **Branch policy.** All changes go on one feature branch, `feature/ai-readiness-<YYYY-MM-DD>`, created from the default branch. Never commit on the default branch (`main`, `master`, or whatever `origin/HEAD` points to) or on any other branch, and never switch away from the feature branch once you are on it. Changes reach the default branch only through a pull request that the user opens, reviews and merges.
- Never push, never merge, never open the pull request yourself. You prepare it; the user sends it.

## 1. Check the ground

1. Run `command -v ai-readiness`. If it is missing, stop and tell the user to install it: clone https://github.com/Miry075/agentic-sdlc-starter and run `scanner/install.sh`.
2. Run `git status --porcelain`. If there are uncommitted changes, stop: ask the user to commit or stash them first (in an unattended run, report it and end). They must not end up in the feature branch.
3. Find the default branch: `git symbolic-ref --short refs/remotes/origin/HEAD` (drop the `origin/` prefix). If that fails, use `main` if it exists, otherwise `master`.
4. Get on the feature branch:
   - If you were invoked with `branch=<name>` (the `ai-readiness agent` command does this), the branch already exists and you are on it. Check with `git branch --show-current` and do not create or switch branches.
   - Otherwise create it from the default branch: `git switch -c feature/ai-readiness-<today as YYYY-MM-DD> <default branch>`. If that name exists, add `-2`, `-3`.
5. Before **every** commit in this skill, run `git branch --show-current`. If it does not print the feature branch, stop and tell the user. Never commit anywhere else.
6. Record the starting score: `ai-readiness . --json --no-open` (field `score`).

## 2. Apply the safe fixes

1. Run `ai-readiness . --fix --apply --json`. It only creates files that are true for any repository and never overwrites.
2. Read the `notes` of each automatic fix and keep them for the final summary.
3. Commit: `git add -A && git commit -m "AI-readiness: safe fixes from the scanner"`.

## 3. Work through the guided steps

1. Run `ai-readiness . --fix --json`. Take the `guided` steps in the order given (largest `potentialGain` first).
2. For each step, follow its `prompt` exactly, together with the rules above. The `criteria` field lists what the scanner checks.
3. Step-specific guidance:
   - **AGENTS.md**: read the solution or project files, package manifests and CI files to get the real build and test commands. Run them if they are quick and safe. Keep it under 300 lines.
   - **Glossary**: take terms from entity names, enum values, routes, table names and comments. Write a definition only when the code or docs support it; otherwise `TODO(human)`.
   - **Specs**: find work item ids in branch names and commit messages. If an Azure DevOps or Jira tool is available in this session, read the work items from it. Otherwise ask the user for the ids and acceptance criteria.
   - **Evaluation set**: draft questions answered by files that exist, then ask the user to confirm, change or drop each one. Only confirmed questions stay in `evals/questions.yaml`.
   - **ADRs**: only decisions visible in the code, using `docs/adr/0000-template.md`. Under `## Status`, write `Accepted` on its own line only when clearly in force; otherwise `Proposed`.
   - **Templates**: specs need the headings Goal, Acceptance criteria and Non-goals, and ADRs need Status, Context, Decision and Consequences, or the CI check `scripts/check-docs-templates.sh` fails. Run `bash scripts/check-docs-templates.sh .` when the script exists.
   - **CI checks**: if AGENTS.md now exists, run `ai-readiness . --fix --apply --json` again first: the scanner adds the checks itself.
4. After each step, run `ai-readiness . --fix --apply --json` (some fixes depend on files you just wrote), then commit with a message naming the area, for example `AI-readiness: AGENTS.md`.

## 4. Ask the user once

Collect every `TODO(human)` you wrote into one numbered list, grouped by file, and add the evaluation questions waiting for confirmation.

- **Interactive session:** ask them all in one message and wait.
- **Unattended run** (invoked as `/ai-readiness unattended`, started with `claude -p`, or no way to ask the user): do not wait and do not stop early. Finish every step you can, then end your final message with the numbered list under the heading `Questions for you`, followed by: "Answer by resuming this run: `claude --resume <session id>` (the `ai-readiness agent` command printed it), or in one line with `claude -p 'Answers: 1. ... 2. ...' --resume <session id>`. Do not use `claude --continue`: it skips unattended runs and opens a different conversation."

When the answers arrive (in this session or a continued one), replace each TODO with its answer, keep the unanswered ones, commit, then run `ai-readiness . --fix --apply --json` and re-scan.

## 5. Stop and report

Stop when a pass through step 3 no longer raises the score, or when only steps that need the user remain.

1. Run `ai-readiness .` (it opens the HTML report in the browser).
2. Report, in a short table: starting score, final score, points per area.
3. List what is left and why: open `TODO(human)` questions, evaluation questions to confirm, work items to describe.
4. Repeat the notes from step 2 that need action (for example: install `jq`; tell the team that the CI check now fails pull requests that change the structure without updating AGENTS.md).
5. Prepare the pull request, without pushing or opening it:
   - **Source:** the feature branch. **Target:** the default branch.
   - **Title:** `AI-readiness: <starting score> to <final score>`.
   - **Description** (print it in full, ready to paste): the score table, what changed in each area with the files, the open `TODO(human)` questions, the notes reviewers must act on (for example the CI check that now fails pull requests), and the commits (`git log --oneline <default branch>..HEAD`).
   - **Commands for the user:** `git push -u origin <feature branch>`, then either `az repos pr create --source-branch <feature branch> --target-branch <default branch> --title "<title>" --description "<description>"` for Azure DevOps, `gh pr create --base <default branch> --head <feature branch> --title "<title>" --body "<description>"` for GitHub, or the web page of the repository.
