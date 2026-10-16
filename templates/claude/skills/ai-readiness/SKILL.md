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
- Never push and never merge. The user reviews the branch.

## 1. Check the ground

1. Run `command -v ai-readiness`. If it is missing, stop and tell the user to install it (see https://github.com/Miry075/agentic-sdlc-starter/tree/main/scanner): `dotnet pack scanner/src/AiReadiness.Scanner -c Release -o ~/ai-readiness-nupkg && dotnet tool install --global ai-readiness --add-source ~/ai-readiness-nupkg`.
2. Run `git status --porcelain`. If there are uncommitted changes, ask the user whether to continue before touching anything.
3. Create a branch: `git switch -c ai-readiness/<today as YYYY-MM-DD>`.
4. Record the starting score: `ai-readiness . --json --no-open` (field `score`).

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
   - **ADRs**: only decisions visible in the code. `Status: Accepted` only when clearly in force; otherwise `Proposed`.
   - **CI checks**: if AGENTS.md now exists, run `ai-readiness . --fix --apply --json` again first: the scanner adds the checks itself.
4. After each step, run `ai-readiness . --fix --apply --json` (some fixes depend on files you just wrote), then commit with a message naming the area, for example `AI-readiness: AGENTS.md`.

## 4. Ask the user once

Collect every `TODO(human)` you wrote into one numbered list, grouped by file, and add the evaluation questions waiting for confirmation.

- **Interactive session:** ask them all in one message and wait.
- **Unattended run** (invoked as `/ai-readiness unattended`, started with `claude -p`, or no way to ask the user): do not wait and do not stop early. Finish every step you can, then end your final message with the numbered list under the heading `Questions for you`, followed by: "Answer in this conversation with `claude --continue`, or in one line with `claude -p 'Answers: 1. ... 2. ...' --continue`."

When the answers arrive (in this session or a continued one), replace each TODO with its answer, keep the unanswered ones, commit, then run `ai-readiness . --fix --apply --json` and re-scan.

## 5. Stop and report

Stop when a pass through step 3 no longer raises the score, or when only steps that need the user remain.

1. Run `ai-readiness .` (it opens the HTML report in the browser).
2. Report, in a short table: starting score, final score, points per area.
3. List what is left and why: open `TODO(human)` questions, evaluation questions to confirm, work items to describe.
4. Repeat the notes from step 2 that need action (for example: install `jq`; tell the team that the CI check now fails pull requests that change the structure without updating AGENTS.md).
5. Tell the user the branch name and suggest opening a pull request after review.
