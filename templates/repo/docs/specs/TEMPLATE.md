# <Work item ID>: <title>

- **Work item:** <link to Azure DevOps / Jira>
- **Risk tier:** Low | Medium | High  <!-- High = money, personal data, security, or irreversible operations -->
- **Author:** <name> · **Reviewed by:** <name>

## Goal

One or two sentences: what changes for the user, and why.

## Existing acceptance criteria

Behaviour already implemented that this change must not break: the regression baseline. Each with the code that satisfies it today (`file:line`). "None: new implementation" when there are none.

- …

## Acceptance criteria

The additional criteria: what this change adds. Numbered, testable statements. Each one becomes at least one test.

1. Given <context>, when <action>, then <expected result>.
2. …

## Use cases

Each actor or caller, dependent behaviour elsewhere, and related workflows on the same code path. Each item concrete enough to verify.

- …

## Edge cases

- <empty input, concurrent update, timeout, permission denied, …>

## Non-goals

What this work item explicitly does **not** do, so agents don't expand the scope.

- …

## Security criteria

- From the work item's Security Criteria field, or "None stated".

## Business rules involved

- Link to `ai-ready` wiki pages or glossary terms.

## Related ADRs

- [0001](../adr/0001-record-architecture-decisions.md)

## Open questions

- Questions for the product owner. Never guess an answer.
