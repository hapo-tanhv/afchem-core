---
globs: *
---

# Fullstack Standards

## Naming Conventions
- **Database/Backend Models**: `snake_case` (Python), mapped to `camelCase` in JSON.
- **Frontend Interfaces**: `PascalCase` (e.g., `UserAttributes`).
- **Variables**: `snake_case` (Python), `camelCase` (TS/JS).
- **Constants**: `UPPER_CASE` (Both).

## TypeScript vs Pydantic Sync
- When changing a **Pydantic Model**, you MUST update the corresponding **TypeScript Interface**.
- Use shared types where possible or generate them.

## API Response Format
All APIs should return JSON in this standard format:
```json
{
  "data": { ... },
  "error": null,
  "meta": { ... }
}
```

## Tool-Centric Architecture (Backend)
- All external service interactions (e.g., HeyGen API, ZAI API) MUST be encapsulated in **Tools** or **Services**.
- Create distinct functions in `backend/app/services/` or `backend/app/tools/` (if using agentic pattern) that can be easily called by the AI.
- These functions should be stateless and typed with Pydantic.

## Error Handling
- **Backend**: Raise `HTTPException`.
- **Frontend**: Catch errors in `useQuery` or `try/catch` and display Toast notifications.
