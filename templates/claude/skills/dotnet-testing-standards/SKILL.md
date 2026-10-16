---
name: "dotnet-testing-standards"
description: "Unit and integration testing standards for .NET/C# — what to test at which layer, tests in the same commit as the behaviour they cover, testability as a design signal, test structure and naming, and what not to test. Use whenever writing, changing, or reviewing .NET code, and always alongside dotnet-design-principles (which governs structure) and dotnet-change-guardrails (which runs the build and test suite but does not decide what tests should exist). Also use when the user asks what tests a change needs, whether coverage is adequate, why something is hard to test, how to test an aggregate or an application service, or whether to mock a dependency."
---

# .NET testing standards

`dotnet-change-guardrails` runs the suite; `dotnet-design-principles` governs structure. This decides **what tests should exist**. Apply it when choosing an approach, not only after the code is written.

## The rule

Every behaviour change ships with its tests **in the same commit** — or with an explicit, stated reason no test applies (pure config, a generated file, a rename with no behavioural surface). Silence is not a reason. State the reason in the review step so the user can disagree.

Every bug fix starts with a test that reproduces the bug and fails for the documented reason. A fix with no failing test first is a fix you cannot prove.

## What to test, and where

| Layer | Test as | Doubles |
|---|---|---|
| Domain — entities, value objects, aggregate invariants | Pure unit tests | None. If you need a mock to test a domain rule, the rule is in the wrong place. |
| Application — services, handlers, orchestration | Unit tests at the port boundary | Fake or stub the abstractions the layer owns. |
| Infrastructure — EF Core, HTTP clients, messaging | Integration tests | Real database (container or in-memory where honest), real serialisation. Never unit-test a mocked `DbContext` or `HttpClient` — it asserts your mock, not your code. |
| Presentation — controllers, endpoints | Usually nothing | If a controller needs its own test, it holds logic that belongs in Application. |

Cover the use-case checklist, not just the happy path: each actor, each boundary condition, the already-failed and already-completed states.

## Testability is a design signal

Hard to test means badly designed, not "needs a better mocking tool". When a change is hard to test, fix the seam — inject the abstraction, extract the decision from the side effect — rather than reaching for shims, statics, reflection, or making internals public. `[InternalsVisibleTo]` for test access is a smell worth naming, not a solution.

Concretely: a `static` call to the clock, the file system, or configuration inside domain logic; a `new`-ed up dependency; a sealed type with no interface at a boundary you need to control. Each is the reason the test is awkward.

## Structure

- Arrange / Act / Assert, one behaviour per test. Multiple assertions are fine when they describe one outcome.
- Name the behaviour, the condition, and the expectation — `Withdraw_WhenBalanceBelowAmount_ThrowsInsufficientFunds`, not `TestWithdraw2`.
- Use the framework, assertion library, and fixture conventions already in the repo (guardrails Phase C). Do not introduce a new test framework or project without asking first.
- Build test data with a builder or factory when the object is non-trivial, so a new required field doesn't break fifty tests.

## What not to do

- Don't assert implementation details — call counts and internal ordering that no caller depends on. The test should survive a refactor that preserves behaviour.
- Don't share mutable state between tests, or depend on execution order.
- Don't chase a coverage percentage. Cover the behaviour and the edges; an untested `get`-only property proves nothing either way.
- Don't backfill tests for code this change didn't touch — name it as follow-up instead.
- **Health sector: never real patient data in tests, fixtures, or seed scripts.** Synthetic values only, including in test names and assertion messages.

## Minimal footprint

The smallest set of tests that actually pins the changed behaviour and its edges. No test for a trivial delegation, no parameterised matrix over cases that cannot occur, no abstraction layer in the test project that the test count doesn't yet justify.