---
description: Technical Design and GitHub Issue Generation
---

# Modular Workflow: Plan

**Slash Command:** `/plan`

This workflow takes the specs and turns them into a technical design and actionable GitHub Issues.

## Steps

1.  **Technical Design**:
    -   Review `specs/{{feature}}/behavior.feature`.
    -   Draft `specs/{{feature}}/design.md`:
        -   **DB Schema**: SQL tables or ORM models.
        -   **API Contract**: Endpoints, Methods, Payloads.

2.  **Task Breakdown**:
    -   Parse Gherkin Scenarios into implementation tasks.
    -   Group tasks by Backend / Web / Mobile.

3.  **GitHub Sync (Auto-Action)**:
    -   For each task:
        -   Call `mcp_github_create_issue` with title, body (referencing spec), and labels.
        -   Collect created Issue URLs.

4.  **Output**:
    -   `specs/{{feature}}/design.md`
    -   List of **Created GitHub Issues**.
