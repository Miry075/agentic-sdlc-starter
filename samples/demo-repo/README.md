# Demo repository

Sample data for the Knowledge Gateway in dev mode. Nothing here is real: the people, the addresses and the numbers are made up.

| Path | Used by |
|---|---|
| `docs/domain/glossary.md` | `get_glossary_term` |
| `docs/adr/` | `search_adrs` |
| `docs/specs/42.md` | `get_spec` |
| `workitems/*.json` | `get_work_item` (a stand-in for Azure DevOps) |

Work items: **41** is `New` (requirements and design), **42** is `Active` (build and test), **43** is `Closed` (no phase).
Work item 41 has a comment with an e-mail address, so you can watch the redaction work.
