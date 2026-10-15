# Domain glossary

> Business terms, acronyms and internal product names. Agents can't guess these.
> Use each term **exactly** as written here in code, APIs and docs.
> **Owner:** <product owner> · **Last reviewed:** <YYYY-MM-DD>

| Term | Definition | In code | Not to be confused with |
|---|---|---|---|
| **Invoice** | A legal document requesting payment for delivered goods or services. Immutable once issued. | `Invoice` | *Quote*: a proposal, not a payment request |
| **Credit note** | A document that cancels all or part of an issued invoice. The only way to correct an invoice. | `CreditNote` | *Refund*: the money movement, which may follow a credit note |
| **Billing cycle** | The period covered by one invoice for a subscription customer. Monthly by default. | `BillingCycle` | *Payment term*: delay allowed to pay an invoice |
| **VAT** | Value-added tax. Rate depends on the customer's country and status (B2B or B2C). | `VatRate` | — |
| **SEPA** | Single Euro Payments Area: the EU scheme for euro bank transfers and direct debits. | `SepaMandate` | — |

## Acronyms

| Acronym | Meaning |
|---|---|
| AC | Acceptance criteria |
| ADR | Architecture decision record |
| PII | Personally identifiable information |

## Adding a term

1. Add a row in alphabetical order.
2. Write the definition so that a new colleague could apply it without asking.
3. Fill *Not to be confused with* when a similar term exists; that is where agents make mistakes.
