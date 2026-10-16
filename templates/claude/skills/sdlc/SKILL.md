---
name: sdlc
description: Run the next step of the agentic SDLC for a work item, following azure-workitem-flow - requirements, design, build, test (review and commit), deliver (pull request and resolve), or review feedback. Use on /sdlc <work item id or dev.azure.com work item URL>, or when asked "what's next for work item 1234".
---

# SDLC: pick the next phase

Given a work item id or URL, find where it stands and run the matching phase skill. One phase per invocation, then stop for the user.

| What exists | Next |
|---|---|
| No branch `feature/<id>-*` and no `docs/specs/<id>.md` | `/requirements-phase <id>` (fetch, branch, analysis, use cases) |
| Spec without a confirmed existing-AC baseline, or with blocking open questions | Stop: list what the user or product owner must answer |
| Spec without a `## Design` section | `/design-phase <id>` (options, user's choice) |
| Design, no implementation on the branch | `/build-phase <id>` (go-ahead, implementation, tests) |
| Uncommitted implementation, or no `## Verification` section | `/test-phase <id>` (verify, review, commit) |
| Committed, no pull request | `/deliver-phase <id>` (pull request, hours, Resolved) |
| Pull request with unresolved review threads | `/deliver-phase <id>`, review comments part (`resolve-review-feedback`) |
| Pull request open, item Resolved | Nothing: merging and closing belong to the reviewers |

Releases are not per work item: use `/release-phase <version>`.

## How

1. Resume from the repository, not from memory: `git rev-parse --abbrev-ref HEAD`, `git status`, `git branch --list "feature/<id>-*"`, `git log <base>..HEAD --oneline`, the spec and its sections, and the pull request through the Azure DevOps tools. State what you inferred before acting, so the user can correct it.
2. Read the work item state. If it does not fit the phase (for example Closed), stop and say so.
3. Say in one sentence which phase you will run and why, then follow that skill exactly, gates included.

This is `azure-workitem-flow` split into resumable phases, with the knowledge gateway and the phase policy added. The companion skills (`dotnet-change-guardrails`, `dotnet-design-principles`, `dotnet-testing-standards`, `resolve-review-feedback`) apply as each phase says.
