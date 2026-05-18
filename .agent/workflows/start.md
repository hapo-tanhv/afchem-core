---
description: Start a new task or clarification process
---

# Global Triage & Clarification

**Slash Command:** `/start`

This workflow acts as the global entry point for all requests. It clarifies requirements and routes to the appropriate specific workflow.

## Phase 0: Requirement Clarification

1.  **Context Gathering**:
    -   Ask the user: "Context của tính năng/bug này là gì?"
    -   Ask the user: "Output mong muốn cụ thể là gì?"
    -   Ask for relevant files or error logs if applicable.

2.  **Assessment & Routing**:
    -   Analyze the complexity of the request.
    -   **Complex / New Feature** -> Route to **`/brainstorm`** (Start of Phase 1).
        -   *Note*: The pipeline will flow: `/brainstorm` -> `/research` -> `/spec` -> `/plan` -> `/build`.
    -   **Simple / Bug Fix** -> Route to **`/quick-change`**.
    -   **Route to `/quick-change`** if:
        -   It's a bug fix.
        -   It's a minor UI tweak.
        -   It's a simple refactor (< 1 hour).
        -   Complexity is Low.

3.  **Handoff**:
    -   Explicitly suggest the next command to run (e.g., "Based on your request, please run `/new-feature` to proceed.").
