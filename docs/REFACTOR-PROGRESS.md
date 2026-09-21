# Refactoring progress

## Scope and baseline

- Starting commit: `5fa9e0241249eab8235ba8ff364556dddf18f74d` (`main`).
- Existing untracked `docs/REFACTOR-AUDIT.md` belongs to the preceding audit and is preserved separately.
- Read the full audit and verified food validation, catalogue seed configuration, and their callers against the implementation.
- Local checkpoints only; no push, deployment, production access, or migration changes.

## Completed: food validation boundary (A8)

Added `FoodValidator` and a result that carries normalized values and field errors without MVC dependencies or input mutation. Kept `ValidationRules.ValidateFood` as the small existing Razor adapter; Community Foods now uses the result directly. Partial normalization and accumulated binding errors remain unchanged.

- Added 13 direct cases covering units, portions, invalid input, overflow, partial normalization and the MVC adapter.
- Baseline: 713 .NET / 41 Node tests passed; ESLint passed.
- Targeted Foods/API-food/Community/Ownership run: 193 passed.
- Diff inspected and `git diff --check` passed. Checkpoint commit follows this log entry.
- Next: isolate cosmetic seeds, verifying zero EF model change.

## Planned next packages

1. Isolate unchanged cosmetic seeds; check EF model equivalence.
2. Inspect and extract a substantive presentation/domain boundary (wardrobe or Diary), with regression tests.
3. Full .NET/Node, Release build, lint, EF model check and final diff review.

## Deferred until verified

CSS reordering requires a reliable visual baseline. Identity onboarding requires executable external-login coverage. Neither will be changed mechanically.
