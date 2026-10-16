---
name: sdlc
description: Run the next SDLC phase for a work item (requirements, design, build, test or release) based on what already exists. Use on /sdlc <work item id>, or when asked "what's next for work item 1234".
---

# SDLC: pick the next phase

Given a work item id, find where it stands and run the right phase skill. Run one phase per invocation, then stop and let the user review.

| What exists | Next phase |
|---|---|
| No `docs/specs/<id>.md` | `/requirements-phase <id>` |
| Spec with unanswered blocking open questions | Stop: list the questions for the product owner |
| Spec, no `## Design` section | `/design-phase <id>` |
| Design, no implementation commits on `feature/<id>-*` | `/build-phase <id>` |
| Implementation, no `## Verification` section | `/test-phase <id>` |
| Verified | Tell the user the PR is ready to open or merge |

Releases are not per work item: use `/release-phase <version>`.

## How

1. Read the work item (gateway `get_work_item`, or the tracker tool) and its state. If the state does not allow the phase you picked (for example Closed), stop and say so.
2. Check the spec, its sections, and `git branch --list "feature/<id>-*"` with `git log`.
3. Tell the user which phase you will run and why, in one sentence, then follow that phase's skill exactly.

The branch, honesty and pull request rules of the phase skills apply.
