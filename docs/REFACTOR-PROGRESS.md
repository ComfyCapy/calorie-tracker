# Refactoring progress

## Scope and baseline

- Starting commit: `5fa9e0241249eab8235ba8ff364556dddf18f74d` (`main`).
- Existing untracked `docs/REFACTOR-AUDIT.md` belongs to the preceding audit and is preserved separately.
- Read the full audit and verified food validation, catalogue seed configuration, and their callers against the implementation.
- Local checkpoints only; no push, deployment, production access, or migration changes.

## Completed: food validation boundary (A8)

Added `FoodValidator` and a result that carries normalized values and field errors without MVC dependencies or input mutation. Kept `ValidationRules.ValidateFood` as the small existing Razor adapter; Community Foods now uses the result directly. Partial normalization and accumulated binding errors remain unchanged.

- Added 12 direct cases covering units, portions, invalid input, overflow, partial normalization and the MVC adapter.
- Baseline: 713 .NET / 41 Node tests passed; ESLint passed.
- Targeted Foods/API-food/Community/Ownership run: 193 passed.
- Diff inspected and `git diff --check` passed. Checkpoint commit follows this log entry.
- Commit: `a8772c2`.

## Completed: cosmetic seed isolation (A2)

Moved the exact 51 seed entries to `Data/CapyItemCatalogue.cs`, returning fresh objects for model construction. `ApplicationDbContext` loses 415 lines of catalogue content; relationships and all migration files remain unchanged.

- Added full seed parity test between `EnsureCreated` and the released migration chain.
- Added `HasPendingModelChanges` regression check: false.
- Targeted Database/Capy/Migration suite: 56 passed.
- Commits: `18c9098`; whitespace-only correction `3896c5a` (no history rewritten).

## Completed: wardrobe boundary

Extracted equipment validation, ownership checks, saved-outfit capture/application/deletion into `CapyWardrobeService`. The PageModel retains authentication, input validation, HTTP/JSON responses, messages and optional progression hooks. Provisioning order, required-layer fallbacks and optional-layer clearing remain unchanged. Registered the concrete scoped service; no new interface or persistence wrapper.

- Added nine direct service cases, including a migrated-database round trip, cross-user rejection, unavailable/category-invalid slots, persistence and removal.
- Updated three existing PageModel test factories for constructor injection; assertions remain unchanged.
- Targeted Capy/ProgressionDomainHook/Database suite: 89 passed.
- Diff inspected; whitespace check passed. Commit: `ae31456`.

## Final validation and handoff

- Complete Release .NET suite: 736 passed, zero failed/skipped (23 added cases over baseline).
- Complete Node suite: 41 passed; ESLint passed.
- Release solution build: succeeded, zero warnings/errors.
- EF `HasPendingModelChanges`: false; full migrated/model seed parity passed. No migration files or schema changed; no local application database was migrated.
- Accumulated diff reviewed and `git diff 5fa9e0241249eab8235ba8ff364556dddf18f74d --check` passed.
- Approximately 1,040 added / 656 deleted lines across 15 files, including tests and this log; much of the seed diff is unchanged content relocated.
- Only pre-existing `docs/REFACTOR-AUDIT.md` remains untracked after checkpoints. No push, deployment, production access, UI changes, or new dependencies.
- Manual local smoke review recommended: food create/edit validation and community submission; Capy equip/unequip, save/apply/delete outfits, preview and progression feedback. Automated persistence/ownership coverage passes; no browser verification was performed in this pass.
- Next substantive package: characterize Diary create/edit measurement policy differences, especially historical snapshots, before extraction. Continue from these checkpoints rather than treating every audit recommendation as mandatory.

## Deferred until verified

## Diary continuation: characterization checkpoint

- Read Create/Edit handlers, existing Diary/snapshot/prepopulation/USDA tests, unit conversion and approximation helpers. Relevant baseline: 138 passed.
- Added 47 PageModel contract cases; all pass against unchanged application code. Coverage includes exact field keys/messages, positive fractional quantities, nonpositive quantities, conversion/portion/estimate overflow, missing selection, invalid mode/estimate, mode-specific binding-error removal, redirects, persisted quantities, foreign/non-original deleted food rejection, foreign portions, and deleted historical portion reconstruction.
- Confirmed deliberate policy differences: Create alone changes Portion to Exact when all portions are deleted; Edit permits only the original deleted food/portion. Edit reconstructs logged portion/estimate amounts and retains nutrition/labels, while exact conversion uses the current food unit even when the snapshot unit differs. Direct-portion foods bypass unit conversion. These behaviors must not be unified away.
- Existing tests additionally cover replacement-food authoritative snapshots, prepopulation, ownership, USDA volume/gram authority and maintenance snapshots.
- Next: extract shared validation/calculation only, with eligible portions and historical amount selection supplied by the PageModels; leave snapshots, binding state application, authorization and persistence at the page boundary.

CSS reordering requires a reliable visual baseline. Identity onboarding requires executable external-login coverage. Neither will be changed mechanically.

Diary measurement extraction remains a separate high-risk package: create/edit share calculations but historical snapshots make their policies non-identical. Profile input processing and React state decomposition also remain open. This pass deliberately completes three isolated boundaries rather than beginning an unvalidated broad rewrite. Existing progression, external-food authority, deployment, and ownership policies are preserved.
