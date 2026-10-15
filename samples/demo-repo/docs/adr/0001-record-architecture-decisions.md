# 0001. Record architecture decisions in the repo

- **Date:** 2026-01-01
- **Deciders:** Tech lead, architects

## Status

Accepted

## Context

Architecture decisions are scattered across meetings, chats and people's memory. New team members and AI coding agents cannot find them, so they reintroduce patterns we already rejected.

## Decision

We record every significant architecture decision as a markdown file in `docs/adr/`, numbered sequentially, using `0000-template.md`. A decision is significant when it affects the structure, a dependency, a contract between services, security or data handling.

ADRs are changed through pull requests. An accepted ADR is never edited to reverse it; a new ADR supersedes it.

## Consequences

- Agents and people find decisions in one place, versioned with the code.
- Reviews can point to an ADR instead of re-discussing a decision.
- Writing an ADR adds about 30 minutes to a structural change.

## Alternatives considered

| Option | Why not |
|---|---|
| Wiki pages | Not versioned with the code; drift from reality; agents can't tell which version applies |
| No formal record | The current situation; decisions get lost |
