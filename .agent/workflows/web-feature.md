---
description: Sub-workflow for Web Implementation
---

# Web Feature Implementation

**Slash Command:** `/run-web-feature`

This is a sub-workflow triggered by `/build` (Step 5), focusing on Next.js implementation.

## Steps

1.  **API Client**:
    -   Update API client hooks using TanStack Query.
    -   Match interfaces with Backend Pydantic models.

2.  **Components**:
    -   Create reusable components in `frontend/components/`.
    -   Use Tailwind CSS for styling.

3.  **Pages**:
    -   Implement pages in `frontend/app/`.
    -   Connect UI to API hooks.

4.  **Validation**:
    -   Check responsiveness.
    -   Check accessibility (basic).
