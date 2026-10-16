---
name: legacy-discovery
description: Reverse-engineer a legacy application into a catalog of business rules and use cases, each with code evidence and a confidence level, for the user to validate before any rewrite. Read-only on the code. Use on /legacy-discovery, or when asked to "reverse engineer", "extract the business rules", or "document what this legacy app does".
---

# Legacy discovery: business rules and use cases

You read a legacy application and write down what it does, in business terms, with proof from the code. The user validates every rule and use case before anything is rebuilt. You change no application code.

## Rules that override everything below

- **Evidence or nothing.** Every rule and use case cites where it lives (`file:line`, stored procedure name, config key). No citation, no entry.
- **Confidence on every entry:** `Certain` (the code says it explicitly), `Inferred` (follows from the code but not stated, say from what), `Unclear` (contradictory or incomplete: it becomes a question). Never upgrade an entry to make the catalog look complete.
- **Describe behaviour, not intent.** Write what the code does today, including what looks like a bug: "Discount is applied twice when X" is a finding, with status Proposed, not something you silently correct. Bugs and dead code go in the questions.
- **Business language.** Rules read like a business analyst wrote them ("A credit note cannot exceed the invoice total"), with the code location beside them, not a paraphrase of the method.
- **Read-only.** Write only under `docs/legacy/`. Never run the application, migrations or scripts against a database. Never print connection strings, credentials or real personal data; if you meet patient or personal data in the code, test data or scripts, note the location only.
- **Branch policy.** Work on `feature/legacy-discovery-<YYYY-MM-DD>` created from the default branch. Never commit on the default branch, never push, merge or open the pull request yourself: the user validates through the pull request.
- **Read efficiently.** Start from `git ls-files`, project and solution files, and `grep -l`; read whole files only when they hold logic. Skip `bin/`, `obj/`, `packages/`, generated code (`*.designer.cs`, `Reference.cs`, `*.edmx` diagrams, T4 output), minified scripts and third-party libraries.

## 1. Map the system

Find, for a .NET Framework application:

| Look for | Where |
|---|---|
| Projects, target frameworks, dependencies | `*.sln`, `*.csproj` / `*.vbproj`, `packages.config` |
| Entry points | WebForms pages (`*.aspx` + code-behind), MVC controllers, Web API controllers, WCF contracts (`[ServiceContract]`, `*.svc`), ASMX, WinForms forms, console apps, Windows services, scheduled tasks |
| Business logic | services, managers, "BLL" projects, code-behind event handlers, validators, `Global.asax` |
| Data access | Entity Framework (`DbContext`, `.edmx`), ADO.NET (`SqlCommand`), Dapper, typed DataSets, and the SQL itself |
| Database logic | `*.sql`, database projects (`*.sqlproj`): stored procedures, triggers, views, functions, check constraints, default values |
| Configuration that changes behaviour | `web.config` / `app.config` `appSettings`, config transforms, feature switches |
| Integrations | outbound HTTP/SOAP clients, service references, file drops, queues (MSMQ), e-mail, FTP, scheduled imports and exports |
| Security | authentication mode, roles and `[Authorize]`, `location` rules in `web.config`, row-level checks in code or SQL |

Write `docs/legacy/README.md`: purpose of the system in two sentences (Inferred unless stated somewhere), the module map, entry points by type, data stores, integrations, and a short "how this catalog was built" note with the date and the commit.

## 2. Extract the catalog

Write these files, with stable ids so the user can refer to them:

- `docs/legacy/glossary.md`: business terms from table and column names, enums, screen labels, resource files and comments. `| **Term** | Meaning | Where found | Confidence |`.
- `docs/legacy/business-rules.md`: one row per rule. `| ID | Rule | Evidence | Confidence | Status | Comment |`, ids `BR-001`, `BR-002`... Group by domain area. Include validations, calculations, state transitions, permissions, limits, defaults, and rules enforced only in the database (triggers, constraints, stored procedures). Status starts as `Proposed`.
- `docs/legacy/use-cases.md`: one section per use case, ids `UC-001`... Each with: actor, trigger (screen, API, job, schedule), main flow, alternative and error flows the code handles, the business rules involved (by id), data read and written, evidence, confidence, and `Status: Proposed`.
- `docs/legacy/data-model.md`: the entities and their relationships in business terms, with the tables behind them. A Mermaid `erDiagram` when it helps.
- `docs/legacy/integrations.md`: each external system, direction, protocol, data exchanged, frequency, and failure handling found in the code.
- `docs/legacy/questions.md`: numbered `Q-001`... Contradictions, likely bugs, dead code (unreachable pages, unused procedures), rules that need business knowledge, and every `Unclear` entry. Each with the evidence and what the answer changes.

Commit per file: `docs: legacy <area> catalog`. Check `git branch --show-current` before every commit.

## 3. Hand over for validation

Print a summary: counts of rules and use cases by confidence, the areas covered and not covered, the top questions, and the pull request to open (`docs: legacy discovery catalog`, target the default branch) with a description that explains the validation process:

- In `business-rules.md` and `use-cases.md`, set each `Status` to `Validated`, `Rejected` (the behaviour is wrong or obsolete: say why in `Comment`), or `Change` (keep the use case, change the rule: say how).
- Answer the questions in `questions.md`.
- Merging the pull request means the catalog is validated.

Stop there. Do not start any implementation.

## 4. After validation (only when the user asks)

When the catalog is merged and the user asks to proceed, read it and propose the work items for the rewrite: one per validated use case, in a sensible order (dependencies first). For each: title, the validated rules as **existing acceptance criteria** (the behaviour the new system must keep), the `Change` rules as **additional acceptance criteria**, and the related questions' answers. Show the list and create the work items in Azure DevOps only after the user approves it. Then each one goes through `/sdlc <id>`.

Rejected rules are not carried over; list them in the pull request of the first work item that would have used them, so reviewers see they were dropped on purpose.
