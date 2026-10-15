# AGENTS.md

> Canonical context for AI coding agents working in this repo.
> Keep it short and true. CI fails if the project structure changes without an update to this file.
> **Owner:** <team name> · **Last reviewed:** <YYYY-MM-DD>

## What this repo is

<One or two sentences: what the service or product does, and for whom.>
Example: *Billing API: computes invoices and payment schedules for B2B customers. Consumed by the customer portal and the ERP integration.*

## Build, test, run

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build
dotnet run --project src/<Project>.Api
```

- Run the full test suite before proposing a change: `dotnet test`
- Format: `dotnet format --verify-no-changes`

## Layout

| Path | Purpose |
|---|---|
| `src/<Project>.Api` | HTTP endpoints only. No business logic. |
| `src/<Project>.Domain` | Business rules. No dependency on infrastructure. |
| `src/<Project>.Infrastructure` | Database, messaging, external APIs. |
| `tests/` | One test project per source project. |
| `docs/adr/` | Architecture decisions. Read before changing structure. |
| `docs/specs/` | One spec per work item. Read the spec before coding. |
| `docs/domain/glossary.md` | Business terms. Use them exactly in code and names. |

## Conventions

- Dependencies point inwards: `Api → Domain ← Infrastructure`. Domain never references Infrastructure.
- Async all the way; pass `CancellationToken` on every I/O call.
- New business rules need a unit test named after the rule.
- Public API changes need an update of the OpenAPI contract and a note in the PR.

## Never

- Never read or edit `.env`, `appsettings.*.Local.json`, `seeds/`, `testdata/` or database dumps.
- Never put real customer data in tests. Use builders in `tests/Builders/`.
- Never change a migration that has already been merged. Add a new one.
- Never add a NuGet package without stating why in the PR description.

## Where to find more

- Architecture decisions: `docs/adr/`
- Business rules: wiki pages labelled `ai-ready`, through the knowledge gateway
- Current task: the work item and its spec in `docs/specs/<work-item>.md`
