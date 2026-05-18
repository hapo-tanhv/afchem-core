---
description: Sub-workflow for Backend Implementation
---

# Backend Feature Implementation

**Slash Command:** `/run-backend-feature`

This is a sub-workflow triggered by `/build` (Step 5), focusing on FastAPI implementation.

## Steps

1.  **Models**:
    -   Review `specs/{{feature_name}}/design.md`.
    -   Update Pydantic models in `backend/app/schemas/`.
    -   Update SQLAlchemy models in `backend/app/models/`.

2.  **Logic**:
    -   Implement business logic in `backend/app/services/`.
    -   Ensure error handling and logging.

3.  **API**:
    -   Create or update routers.
    -   Register new routers in `backend/app/main.py`.

4.  **Testing**:
    -   Create `backend/tests/test_{{feature_name}}.py`.
    -   Run `pytest backend/tests/test_{{feature_name}}.py`.
