---
name: requirements-phase
description: Turn a new work item into a committed spec with testable acceptance criteria. Use when a work item is in the New state and has no spec yet.
---

# Requirements phase

You are turning a work item into a spec. Pass `phase: "requirements"` on **every** knowledge gateway tool call.

## Steps

1. Read the work item: `get_work_item(phase: "requirements", id: <id>)`.
2. Find the business rules that apply: `search_product_docs(phase: "requirements", query: <topic>)`.
3. Check every business term: `get_glossary_term(phase: "requirements", term: <term>)`.
4. Draft `docs/specs/<work-item>.md` from `docs/specs/TEMPLATE.md`:
   - acceptance criteria as numbered *Given / When / Then* statements;
   - edge cases;
   - explicit non-goals;
   - risk tier (High for money, personal data, security or irreversible operations).
5. List open questions for the product owner instead of guessing answers.

## Rules

- Never invent a business rule. If no `ai-ready` page covers it, write it as an open question.
- Never include personal data or real customer examples in the spec.
- The spec is a draft until a human reviews and merges it.

## You do not have

The code graph (L2) and ADRs are not available in this phase. Describe *what* must happen, not *how* to build it.
