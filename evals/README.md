# Evaluation set

Without measurement, you don't know if your knowledge layer helps or hurts. This folder holds a small set of real questions with the source an agent must use to answer each one.

## Writing good questions

- **Use real questions.** Collect them from code reviews, onboarding sessions and chat ("where is…", "why do we…", "can I…").
- **Cover every layer and phase.** At least 5 questions each for the glossary, ADRs, business rules, code structure and the current task.
- **Add negative tests.** Questions the agent must refuse (secrets, personal data, layers outside its phase).
- **Name the expected source**, not only the expected answer. An agent that gives the right answer from the wrong source will fail next time.

## Running it

Until the evaluation runner is published, run it manually or with a small script:

1. For each question, start a fresh agent session in the declared phase.
2. Ask the question.
3. Pass if the answer cites an expected source **and** contains every `mustContain` term. For `expectRefusal`, pass if the agent refuses or reports no access.
4. Record the score in your PR or pipeline run.

## When the score drops

Find the failing questions, then fix the **source**: rewrite the wiki page, update the ADR, add the glossary term. Never patch a generated index by hand; the next rebuild brings the error back.
