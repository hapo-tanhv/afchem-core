---
description: Sub-workflow for Mobile Implementation
---

# Mobile Feature Implementation

**Slash Command:** `/run-mobile-feature`

This is a sub-workflow triggered by `/build` (Step 5), focusing on React Native implementation.

## Steps

1.  **Navigation**:
    -   Add new screens to Navigation Stack/Tabs.
    -   Define Types for route params.

2.  **Screens**:
    -   Implement screens using React Native components.
    -   Style with **NativeWind**.

3.  **Logic**:
    -   Implement custom hooks for logic.
    -   Connect to Global Store (Zustand) if needed.

4.  **Testing**:
    -   Run tests using Maestro logic (if applicable) or manual verification on simulator.
