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

## Binding continuation (starting at `49e9178`)

- Added five executable HTTP/antiforgery Profile tests before changing binding: posted identity/navigation keys are ignored, current-user ownership is used, original validation keys/messages render, and theme is accepted on create but preserved on update.
- Replaced the bound EF entity with page-local `ProfileInput`, keeping the property name `UserProfile` and exact editable-field annotations/defaults. Explicit mapping omits IDs/navigation. GET estimates and persistence still use entities; no view or model/schema changes.
- Updated direct PageModel test construction to map inputs explicitly. Existing assertions retained; the old direct-assignment overposting example is now backed by actual HTTP tests.
- Targeted Profile/Theme/GoalTimeline/DailyMaintenance/ProgressionDomainHook suite: 151 passed. Diff reviewed; checkpoint follows. Continue directly to Identity executable onboarding coverage.
- Binding checkpoint: `f9d4d3a`.

## Identity onboarding continuation

- Added executable external-confirmation tests using protected Identity external cookies, real Identity stores and the test email sender. Successful linking grants exactly Standard/starter cosmetics and emits a verifiable confirmation token; a failed login link produces no role/provisioning/email for the new account. Both cases passed before extraction.
- Extracted shared starter provisioning/token generation/branded email into `AccountOnboardingService`. Entry pages retain Identity create/link outcomes, explicit Standard-role assignment, callback URL differences (password flow includes returnUrl), confirmation/sign-in/redirect decisions and failure ordering. No authentication architecture changes or real provider calls.
- Identity/Community/Email/Capy targeted suite: 100 passed. Diff reviewed; local checkpoint follows. Continue to conservative React presentation decomposition; request/state handling will remain intact.
- Checkpoint: `7d03437`.

## React presentation continuation

- Extracted the unchanged results toolbar, tiles/actions and pagination into `FoodSearchResults`; App retains all state, API calls, latest-request behavior and navigation. No extra dependency or hook/state rewrite.
- Added rendered accessibility/busy-state and callback-argument tests. Node: 42 passed; ESLint and Vite production build passed. Rebuilt checked-in island output. Foods discoverability suite: 10 passed before final extraction correction; rerun with full suite at handoff.
- Diff reviewed; checkpoint follows. Continue to exact-order CSS splitting; no declaration deletion or visual redesign.
- Checkpoint: `5854ff0`. App reduced by 157 lines; state/request extraction is not required for this presentation boundary.

## Conservative CSS continuation

- Split existing top-level contiguous sections into `site.css` (legacy/base/component layers), `shell-scenic.css` (design system/shell/hero), and `dashboard-profile.css` (later dashboard/profile/compact overrides). All remain in the same URL directory; no relative asset URLs were present.
- Layout loads them in exact original order between Bootstrap and generated scoped styles, with existing fingerprinting/version behavior. No declaration changed/deleted/reordered. Recombined normalized content matches original SHA-256 `19F36FA12F15ADF52CB7B5402047E7FC46283CBFDAF3E738AA58DB8214687BCB`.
- Added executable served/fingerprinted stylesheet-order and content-hash regression test. Updated dashboard CSS assertion to read all loaded parts. Targeted Theme/Dashboard/FoodSearchDiscoverability/ProgressionActivityPageFilter suite: 90 passed.
- Visual deletion/deduplication is not attempted: remaining overrides are intentional until proven otherwise. Desktop/mobile light/dark smoke review recommended, but exact content/order proof permits continuing with independent audit items.

## Interrupted Admin package recovered

- Resume inspection: Profile binding `f9d4d3a`, Identity `7d03437`, React `5854ff0` and CSS `a0ad976` were committed; only Admin Users query change and its new test were uncommitted. No interrupted work was discarded.
- B8 replaces up to 30 per-user role queries with one bounded membership/role join after the existing filtered/ordered 30-user query. Mutation handlers and authorization remain unchanged.
- Strengthened the interrupted test with a command interceptor asserting exactly two list queries, plus filtering, limit, ordering, no-role users and exact Identity role-list parity. Community suite: 41 passed; focused enhanced query-count test passed. Diff/whitespace inspected; local checkpoint follows.
- Checkpoint: `19346ae`.

## CI verification gate

- A10: added `.github/workflows/verify.yml` for push/PR checks using the existing .NET 10/Node 24 stack, lockfile install, Node tests/lint/build, generated-bundle drift guard, Release build and all .NET tests (including migration/model compatibility). Read-only repository permissions; no production credentials or deployment job.
- Local Node tests/lint/build and bundle drift check pass. Workflow structure manually reviewed; optional local YAML parsers are not installed, and no dependency was added just for this check. Hosted runner execution remains unverified until the user pushes; this task does not push.
- Checkpoint: `d8ed870`.

## Static artifact hygiene

- Repeated the authored reference scan: `macro-goals-preference.js` and its three data attributes are absent from application markup/scripts; only negative regression assertions reference them. Removed this 50-line dormant script. It remains recoverable from Git.
- Kept `dashboard-hero-capy1.png`: no internal references, but its published URL may have external consumers and this task cannot inspect production traffic. Removing a public compatibility asset is not needed for maintainability closure.
- Targeted macro/dashboard/stylesheet checks passed; no artwork was modified. Local checkpoint follows.
- Checkpoint: `9ff449c`; targeted suite: 50 passed.

## Final A/B disposition (supersedes earlier continuation suggestions)

| Item | Final disposition | Evidence / remaining boundary |
| --- | --- | --- |
| A1 Diary measurement | Completed | Resolver and characterization protect Create/Edit historical differences; snapshots remain page policy. |
| A2 cosmetic seeds | Completed | Exact 51-entry extraction, migrated seed parity and no-model-difference tests. |
| A3 CSS | Completed, conservative scope | Three contiguous stylesheets, exact original declaration-stream hash and served load-order test. No speculative deduplication; further deletion has no demonstrated equivalence and is deliberately excluded. |
| A4 Profile | Completed | Pure processing plus dedicated HTTP input, unchanged annotations/names, executable overposting and rendered-validation tests. |
| A5 wardrobe | Completed | Focused equip/outfit service with ownership, unavailable-slot and persistence coverage. |
| A6 React | Completed, narrowed after inspection | Results/actions/pagination separated, request/state machine retained. Splitting the shared request-sequence state into a hook now mostly relocates coupled state rather than simplifying it; no further module churn justified. |
| A7 Diary browser state | Deliberately deferred | Current VM harness characterizes computation, not DOM focus/keyboard/blur ordering or all initialization/mode transitions. A safe state-machine rewrite requires a real DOM interaction harness first; inventing a broad fake DOM or adding a new browser-test dependency solely for closeout is disproportionate. Existing isolated IIFE and focused functions remain unchanged. This is an executable-coverage gap, not a request for visual redesign. |
| A8 food validation | Completed | MVC-independent validator and small adapter; partial normalization/errors preserved. |
| A9 onboarding | Completed, narrowed | Shared provisioning/token/email service; real external-cookie success/link-failure tests and password-path integration coverage. Role assignment stays explicit at the security-sensitive entry boundary, preserving its order relative to Identity outcomes. |
| A10 CI | Completed locally | Read-only verification workflow, frontend drift guard, all existing suites. First hosted run cannot be verified without the user pushing; no deployment workflow added. |
| B1 legacy USDA route | Deliberately deferred | Unknown bookmarked/external callers; production telemetry is unavailable and production access prohibited. Keep supported route, tests and policies intact. Retirement requires usage evidence and an explicit compatibility decision. |
| B2 favourites | No longer justified as proposed | Audit names a nonexistent `UserFavouriteFood`; actual state is `Food.IsFavourite`. Internal-ID and provider/source-ID lookups plus external duplicate-insert recovery differ. Extracting a Boolean assignment adds indirection or risks hiding ownership/recovery semantics; current explicit operations retained. |
| B3 progression split | No longer justified now | Coherent transaction/award boundary, extensive tests, no catalogue growth requiring decomposition in this campaign. |
| B4 startup extraction | No longer justified now | Composition root remains explicit; new service registrations do not warrant hiding middleware/environment ordering in wrappers. |
| B5 cancellation propagation | Deliberately deferred | Existing tokenless provider APIs and resolver map provider failures/timeouts into unavailable/cached fallback outcomes. Caller-cancellation propagation changes that observable failure contract; needs a separate reliability package with caller-cancel/timeout/fallback tests, not a claim of behavior-identical structural cleanup. No cancellation behavior was silently changed. |
| B6 orphan assets | Completed for dormant script; image deferred | Macro preference script unreferenced and removed. Old public hero URL retained because external hotlinks cannot be ruled out without usage evidence. No artwork changed. |
| B7 source-based tests | Superseded where touched | Executable external onboarding, Profile HTTP and React control/callback tests now provide behavior coverage. Existing source/packaging guards retained rather than removed merely to inflate refactor freedom; no blanket test rewrite justified. |
| B8 Admin role queries | Completed | Two-query interception test plus role-order parity, bounded search and existing authorization/mutation coverage. |

## Campaign closeout

- The behavior-preserving campaign is closed with the explicit deferred follow-ups above; it is not a claim that every possible refactor was implemented. No incomplete source edits remain. No product decision is needed to use/review the current checkpoints; retiring public compatibility URLs would need a separate decision.
- Final local full .NET run: 845 passed, zero failed/skipped, up 132 from the original 713 and up 9 from the 836 pre-binding baseline. Complete Node suite: 42 passed (originally 41). ESLint and Vite production build passed; rebuilt frontend output matches the committed bundle.
- Release solution build: zero warnings/errors. Explicit model/seed compatibility tests: 2 passed; EF reports no pending model change. Historical migration files are unchanged. No application database reset/migration or production access occurred.
- Admin checkpoint `19346ae`, CI checkpoint `d8ed870`, artifact cleanup `9ff449c`; earlier package hashes are recorded above. Accumulated diff reviewed against `5fa9e02`, including security entry ordering, registration-only startup changes, exact CSS order/content and unchanged migrations. Whitespace check passed.
- Meaningful reductions: DbContext -415 lines, ValidationRules -126, Customisation PageModel -89, Diary Create/Edit -308 combined, Profile PageModel -249, App.jsx -141. New focused processors/services/input types and tests account for deliberate growth; 2,482 CSS lines were relocated, not rewritten. No new runtime dependency, framework, project, or architectural layer.
- Manual follow-up: local metric/imperial/goal/target and Diary historical-mode smoke checks; actual configured external-provider handshake; React provider/autocomplete/favourite/pagination interaction; desktop/mobile light/dark layout. These do not block the tested local checkpoint. First CI hosted run needs review when the user chooses to push.
- Repository handoff: tracked work committed locally; pre-existing untracked `docs/REFACTOR-AUDIT.md` preserved unchanged. Nothing pushed or deployed. Removed script can be recovered from Git. Recommended next action: review the local commit series and smoke-test the unchanged UI before choosing whether to merge/push; do not automatically restart the deferred packages.
