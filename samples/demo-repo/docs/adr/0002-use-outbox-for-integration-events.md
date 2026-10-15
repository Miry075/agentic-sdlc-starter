# 0002. Use the outbox pattern for integration events

- **Date:** 2026-02-10
- **Deciders:** Tech lead, architects

## Status

Accepted

## Context

Invoices are saved in the database and announced to other services. Publishing directly to the message bus after the save can lose events when the process stops between the two steps.

## Decision

Integration events are written to an `Outbox` table in the same transaction as the invoice. A background worker publishes them to the message bus and marks them as sent. Consumers must handle duplicates.

## Consequences

- No event is lost when the process stops.
- Events can be delivered more than once, so consumers are idempotent.
- The outbox table needs a retention job.

## Alternatives considered

| Option | Why not |
|---|---|
| Publish after save | Loses events on failure |
| Distributed transaction | Not supported by the message bus |
