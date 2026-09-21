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

## Diary continuation: characterization checkpoint

- Read Create/Edit handlers, existing Diary/snapshot/prepopulation/USDA tests, unit conversion and approximation helpers. Relevant baseline: 138 passed.
- Added 47 PageModel contract cases; all pass against unchanged application code. Coverage includes exact field keys/messages, positive fractional quantities, nonpositive quantities, conversion/portion/estimate overflow, missing selection, invalid mode/estimate, mode-specific binding-error removal, redirects, persisted quantities, foreign/non-original deleted food rejection, foreign portions, and deleted historical portion reconstruction.
- Confirmed deliberate policy differences: Create alone changes Portion to Exact when all portions are deleted; Edit permits only the original deleted food/portion. Edit reconstructs logged portion/estimate amounts and retains nutrition/labels, while exact conversion uses the current food unit even when the snapshot unit differs. Direct-portion foods bypass unit conversion. These behaviors must not be unified away.
- Existing tests additionally cover replacement-food authoritative snapshots, prepopulation, ownership, USDA volume/gram authority and maintenance snapshots.
- Next: extract shared validation/calculation only, with eligible portions and historical amount selection supplied by the PageModels; leave snapshots, binding state application, authorization and persistence at the page boundary.
- Characterization commit: `271767b`; combined targeted suite at that checkpoint: 185 passed.

## Diary continuation: shared measurement resolver

- Added pure `DiaryMeasurementResolver` with submitted measurement input, result quantity/selection, exact field errors and mode-specific binding fields to clear. No database/MVC dependencies or entity mutation. Uses existing conversion/multiplier helpers.
- Create retains no-portions fallback, new-entry metadata/snapshot capture, progression and persistence. Edit retains eligible historical food/portion rules, lazy historical amount selection, food-change snapshot replacement and label retention. Its two local amount functions run only after a valid selection; historical division is not newly caught or evaluated eagerly.
- Reduced Create by 144 lines and Edit by 164 lines. No route, view, script, CSS, DI, model or migration changes.
- Added six further PageModel cases for historical estimate label/direct-serving policy and retained binding errors, plus 17 direct resolver cases for precision/boundaries, legacy nonpositive amounts, lazy policy execution and overflow. Total new cases this run: 70.
- Targeted Diary/Ownership/Measurement/Approximate suite: 208 passed. Node: 41 passed; ESLint passed. Release build: zero warnings/errors. Explicit EF model/seed compatibility checks: 2 passed; no model changes.
- Extraction commit: `f50ca62`. Full .NET suite: 806 passed, zero failed/skipped. Diff reviewed and `git diff dcb7358 --check` passed. No production access, push or deployment.
- Final state: tracked work committed locally; pre-existing `docs/REFACTOR-AUDIT.md` remains untracked and untouched. This run changes only two Diary PageModels, the resolver, two Diary test files and this progress log. No half-refactor remains.

## Profile continuation: characterization

- Starting checkpoint `3d00189`; audit reread, existing tracked work clean and untracked audit preserved.
- Added 23 PageModel cases for option/date/age errors, missing/converted imperial values, goal/weekly requirements, target mode, stale binding-error cleanup, calculated-target validation ordering and the strict 0.01 kg existing-goal tolerance.
- Profile/Theme/GoalTimeline suite: 102 passed against unchanged application code.
- Next: page-specific pure processing input/result while preserving existing Razor binding names/attributes. Binding-contract replacement is deliberately separate from conversion/validation extraction to avoid coupling binder changes to policy changes.
- Characterization checkpoint: `cf07ddf`; original targeted baseline was 79 tests.

## Profile continuation: processing boundary

- Added page-local `ProfileFormInput`, `ProfileFormProcessor` and result/errors. Processing clones only form values, returns canonical values without mutating the submitted/existing entity, and explicitly reports field errors/cleared binding keys. No MVC/EF dependency in processing logic.
- Preserved conversion/goal/cleanup/calculated-target ordering, including case-insensitive incoming ModelState keys, partial normalization and existing-goal tolerance. The PageModel still owns authentication, binding attributes, HTTP responses, estimates, theme endpoint, persistence, snapshots and progression.
- Reduced Profile PageModel by 249 net lines. The processor adds approximately 340 lines including explicit input/output/mapping; this improves testability rather than minimizing total LOC.
- Added seven direct processor cases: non-mutation/identity-safe mapping, binding-error gating, canonical imperial boundaries. Targeted Profile/Theme/GoalTimeline suite: 109 passed. Diff inspected and whitespace check passed.
- This completes the conversion/validation extraction, not the audit's entire binding-model replacement: the public `UserProfile` binding property and its DataAnnotations intentionally remain unchanged. Replacing it needs separate HTTP binding/overposting and rendered validation coverage; do not silently claim that work complete.
- Processing checkpoint: `21fdf9c`. No Identity, React or CSS changes were begun in this pass; retain a validated checkpoint rather than starting another high-risk package with limited remaining budget.

## Profile continuation: final validation / handoff

- Complete .NET suite: 836 passed, zero failed/skipped (30 new cases). Node: 41 passed. ESLint passed.
- Release build: zero warnings/errors. Explicit model compatibility/seed tests: 2 passed; EF detects no model changes. No migrations created or modified.
- Accumulated diff from `3d00189` reviewed; `git diff 3d00189 --check` passed. Five files changed, approximately 585 additions / 259 deletions including tests/log. Production net growth: 90 lines; Profile PageModel reduced by 249 lines.
- Tracked changes are checkpointed locally. Existing untracked audit remains untouched. No push, deployment, production access, or UI changes.
- Recommended continuation: add HTTP form-binding/validation/overposting coverage before considering replacement of the remaining bound Profile entity. Then establish executable external-login onboarding coverage before Identity extraction. React and CSS follow only after backend checkpoints.
- Manual local browser review remains recommended for metric/imperial switching, Maintain/custom-target switching and existing-goal edits; none was performed in this pass.

## Deferred / manual review

CSS reordering requires a reliable visual baseline. Identity onboarding requires executable external-login coverage. Neither will be changed mechanically.

Diary measurement extraction is now implemented with policy differences preserved. Profile input processing and React state decomposition remain open and were out of scope for this continuation. Existing progression, external-food authority, deployment, and ownership policies are preserved. A local browser smoke test of Create/Edit mode switching and historical entries remains recommended; automated prepopulation/rendering tests pass, but no manual browser check was performed.
