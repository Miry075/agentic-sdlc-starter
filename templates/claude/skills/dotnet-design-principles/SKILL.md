---
name: "dotnet-design-principles"
description: "Design-quality standards for .NET/C# code — SOLID, DRY/reuse over duplication, KISS/simplicity, Domain-Driven Design (entities, value objects, aggregates, repositories, bounded contexts), layering and dependency direction, and related conventions (nullable reference types, async correctness, immutability, DI lifetimes, exception design). Use this whenever writing, reviewing, or refactoring .NET code, and always alongside azure-workitem-flow (during the implementation and review steps) and dotnet-change-guardrails (which governs process safety, not design quality — this skill is the one that governs whether the code itself is well-structured). Also use when the user asks whether a design follows SOLID, whether a domain model is anemic, how to structure a new class or module, or how to fix a design smell."
---

# .NET design principles — SOLID, DDD, and related practices

## When this applies

Any time you write, change, or review .NET/C# code. Inside `azure-workitem-flow`, apply this during "Get the implementation go-ahead" and "Review the implementation", alongside `dotnet-change-guardrails`: that skill governs process safety (git, verification, scope, commit hygiene); this one governs whether the code itself is well-structured. They are complementary, not overlapping.

## The precedence rule — read this first

Follow the principles below **in the code you write or change, regardless of what surrounds it.** If the existing code nearby violates them — and in real .NET codebases, especially older ones, it often will — don't copy the violation to match it, and don't take it on yourself to silently refactor it either. Name it as a follow-up in the review step instead, the same way `dotnet-change-guardrails` Phase D asks you to name other risks honestly.

This is a different rule from `dotnet-change-guardrails` Phase C ("follow local convention over general best practice"), which is about *tooling and pattern choices* — DI registration style, logging abstraction, which test framework is already in use. That rule still stands for those choices. This rule is about the structural principles below, which hold independent of local habit.

## SOLID

- **Single Responsibility** — one reason to change. Smell: a class or service accumulating unrelated methods, or a name like `...Manager` / `...Helper` that's grown to mean "everything related to X." Fix: split by responsibility, each behind its own interface.
- **Open/Closed** — extend without modifying. Smell: a `switch` or `if`-chain on a type code that gains a new branch every sprint. Fix: polymorphism, a strategy pattern, or a registered-handler lookup instead of a growing conditional.
- **Liskov Substitution** — a subtype must be usable anywhere its base is expected, no surprises. Smell: an override that throws `NotImplementedException`, narrows what the base allowed, or returns `null` where the base contract never would. Fix: favor composition, or shrink the shared contract to what every implementer can actually honor.
- **Interface Segregation** — don't force a consumer to depend on methods it doesn't use. Smell: a fat interface where most implementations stub out half the members. Fix: split into smaller, role-specific interfaces.
- **Dependency Inversion** — depend on abstractions owned by the consumer, not on concretions. Smell: a domain or application class instantiating an `EF DbContext`, an `HttpClient`, or a concrete repository directly. Fix: inject an interface defined in the layer that needs it; implement it in infrastructure.

## Reuse over duplication (DRY)

Before writing new logic, look for it first. A validator, a mapping, a query pattern, a formatting helper, a piece of domain logic — if something in the codebase already does this or something close to it, extend or reuse it rather than writing a parallel version. Two near-identical blocks of logic drifting apart over time is one of the most common ways .NET codebases rot: the bug gets fixed in one copy and quietly stays broken in the other.

That said, don't force a shared abstraction onto two things that merely look similar today but serve different domains or will diverge for good reason — reaching for "let's extract this so we're not repeating ourselves" the moment two things look alike is its own kind of over-engineering (see Minimal footprint, below). The distinction: reuse when the logic is the same *because the underlying rule is the same*; don't unify when the resemblance is coincidental. When genuinely unsure which it is, say so and ask rather than picking silently.

When reviewing rather than writing (this applies directly inside `dotnet-pr-review`), treat new code that duplicates existing capability as a real finding — name the existing code it should have used instead, with its `file:line`, not just "this looks duplicated."

## Keep It Simple (KISS)

At the level of a single method or class: prefer the plainest solution that correctly solves the problem in front of you. A clever one-liner that takes longer to parse than three obvious lines is a net loss. Generics, reflection, expression trees, or a design pattern (strategy, visitor, decorator) are tools for a specific kind of complexity — reach for them when the problem actually has that shape, not because they're available or because "more abstract" reads as "more professional." Complexity should be justified by what the problem demands, not by what could theoretically be reused someday.

This is the same instinct as Minimal footprint below, one level down: that section is about not adding architecture the change doesn't need; this one is about not adding cleverness the method doesn't need. Both fail the same way — effort spent on generality or elegance nobody asked for, at the cost of a reader's time later.

## Domain-Driven Design

- **Entities vs. value objects.** An entity has identity that persists through change (an order, a patient record). A value object is defined entirely by its data (an address, a date range) — make it immutable and equality-by-value.
- **Aggregates and aggregate roots.** Invariants that span multiple objects are enforced inside the aggregate root's methods, not scattered across application services. If an application service is doing multi-step validation that really belongs to the aggregate's own consistency rules, that's a design smell worth naming.
- **Repositories** are one per aggregate root, not one per table. A repository that returns child entities independently of their aggregate root is usually a sign the aggregate boundary is wrong.
- **Anemic domain model** — the classic .NET anti-pattern: entities are just public-getter/public-setter bags, and all the actual logic lives in services. Watch for it and push behavior back onto the entity or aggregate that owns the data.
- **Ubiquitous language.** Name classes and methods the way the business/domain experts would say them, not generic terms like `Data`, `Info`, `Manager`, `Processor`. A mismatch between code vocabulary and the work item's own language is worth flagging.
- **Domain events** for side effects that need to happen when something changes but that the aggregate itself shouldn't need to know about (notify billing, update a read model) — raise an event, don't reach across the boundary directly.
- **Bounded contexts.** Don't reach into another module's internal entities directly; go through its published contract (its own service/repository interface, an event, or an API), even inside the same solution.

## Layering and dependency direction

Domain depends on nothing else in the solution. Application depends only on Domain's abstractions. Infrastructure implements Domain/Application abstractions (EF Core, HTTP clients, file storage — all live here). Presentation (controllers, endpoints) depends on Application, never reaches into Infrastructure directly. If a Domain project references `Microsoft.EntityFrameworkCore` or similar, that's the violation to flag, not to wave through because "it's just this once."

## Related conventions worth naming, since "and so on" covers real ground

- **Nullable reference types** on and honored — not silenced with `!` out of convenience.
- **Async all the way.** No sync-over-async (`.Result`, `.Wait()`), no `async void` outside event handlers.
- **Composition over inheritance** for reusing behavior; inheritance reserved for genuine is-a relationships.
- **Exceptions describe exceptional situations**, not expected outcomes — a validation failure returns a `Result`/error, it doesn't throw. Custom exception names describe the domain concept (`InsufficientStockException`), not the fact that something went wrong (`ProcessingException`).
- **Immutability** for value objects and DTOs — records or init-only properties, not classes with public setters, when the object represents a value rather than a mutable thing.
- **DI lifetimes matched to state** — singleton for stateless services, scoped for anything touching a `DbContext`. A scoped service captured by a singleton is a common, subtle production bug; flag it when you see it.

## Minimal footprint — this is not a license to over-engineer

The user this skill serves is an experienced .NET developer, not someone who needs SOLID or DDD explained from scratch in every response — apply the judgment, skip the tutorial. And applying it well means the *smallest* change that gets the touched code to a good state, not the occasion to introduce architecture the ticket didn't ask for.

Concretely: don't create a new aggregate, repository, domain event, or bounded-context boundary for a change that doesn't need one. Don't introduce a generic repository abstraction, a mediator/CQRS pipeline, or a new base class hierarchy because SOLID is in scope — that's solving a problem nobody has yet, and it's exactly the over-engineering this section exists to rule out. If the existing shape of the code already satisfies the principle well enough for what's being changed, leave it alone.

Where this does call for real structural change — an anemic entity that's about to get more business logic added to a service instead of to itself, a class picking up a second unrelated responsibility because of this change — make that change, because deferring it makes the code worse, not because deferring it violates a rule. Where it doesn't, don't manufacture one; that's the same discipline `dotnet-change-guardrails` Phase B asks for when it says not to manufacture solution options nobody needs. In the review step, state briefly which principles were actually relevant to this change and how the code satisfies them, and say plainly when a principle didn't apply rather than forcing a mention of it.