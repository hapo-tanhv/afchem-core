---
description: Execution Phase - Code, Test, Release
---

# Modular Workflow: Construction (Build)

**Slash Command:** `/build`

This workflow orchestrates the actual coding implementation, executed after the Plan phase.

## Steps

1.  **Backend Implementation**:
    -   Trigger `/run-backend-feature`.
    -   Implement API & DB changes as per `specs/{{feature}}/design.md`.

2.  **Client Implementation** (Parallel):
    -   **Web**: Trigger `/run-web-feature`.
    -   **Mobile**: Trigger `/run-mobile-feature`.

3.  **Quality Assurance**:
    -   Run E2E Tests (Playwright/Maestro).
    -   Verify against Gherkin Scenarios in `specs/{{feature}}/behavior.feature`.

4.  **Release**:
    -   Run `intelligent-code-review`.
    -   Update output documentation.
