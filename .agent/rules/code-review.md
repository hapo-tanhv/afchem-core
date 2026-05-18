---
globs: *
---

# Intelligent Code Review Rules

When reviewing code (automatically via `intelligent-code-review` workflow or manually), check for:

## 1. Safety & Security
- [ ] No hardcoded secrets (API Keys, tokens).
- [ ] Input validation present (Pydantic models, Zod schemas).
- [ ] No SQL Injection vulnerabilities (ORM usage).

## 2. Performance
- [ ] No N+1 queries in Backend.
- [ ] Proper use of `useMemo`/`useCallback` in React.
- [ ] Images optimized (Next.js Image component).
- [ ] FlatList used for long lists in React Native.

## 3. Clean Code
- [ ] No `any` type in TypeScript.
- [ ] Meaningful variable/function names.
- [ ] DRY (Don't Repeat Yourself) principle.
- [ ] Comments explaining "WHY", not "WHAT".

## 4. Architecture
- [ ] Logic separated from UI components.
- [ ] Service layer used for Business Logic.
