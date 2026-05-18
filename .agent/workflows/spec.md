---
description: Requirement, Behavior Definition and Gherkin Specs
---

# Modular Workflow: Specification

**Slash Command:** `/spec`

This workflow consolidates ideas and research into concrete requirements and behavior specifications (BDD).

## Steps

1.  **Requirement Drafting**:
    -   Synthesize inputs from `docs/{{feature}}/ideas.md` and `docs/{{feature}}/research.md`.
    -   Draft natural language requirements in `specs/{{feature}}/requirements.md`.
    -   **Loop**: Refine with user until confirmed.

2.  **BDD Conversion (Gherkin)**:
    -   Translate requirements into Gherkin Scenarios (Given/When/Then).
    -   Create `specs/{{feature}}/behavior.feature`.
    -   Ensure scenarios cover Happy Path, Edge Cases, and Error States.

3.  **Review**:
    -   Present Scenarios to User.
    -   Confirm: "Do these scenarios accurately describe the desired behavior?"

4.  **Output**:
    -   `specs/{{feature}}/requirements.md`
    -   `specs/{{feature}}/behavior.feature`
