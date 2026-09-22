# Comfy Capy Calories refactoring audit

> **Status:** In progress. This is an architecture and code-quality audit only; no refactoring has been implemented.
>
> **Audit baseline:** Current `main` snapshot `5fa9e0241249eab8235ba8ff364556dddf18f74d`. Preserve current production behavior and keep the existing 713-test .NET baseline (plus 41 Node tests) passing. Recommendations avoid schema changes unless explicitly separated and justified.

## 1. Executive summary

The current architecture is appropriate for the product and deployment: a modular monolith built primarily with Razor Pages, one deliberately isolated React food-search island, EF Core over SQLite, and explicit Linux release/migration operations. The audit found no justification for a SPA rewrite, microservices, CQRS/MediatR, generic repositories, a new database, or a multi-project “Clean Architecture” expansion.

The highest-value work is targeted rather than structural replacement. The main maintenance hotspots are duplicated Diary measurement rules; presentation-bound food validation; large Profile and Customisation PageModels that own domain decisions; the 809-line React search component and 724-line Diary script; a 6,210-line chronological CSS cascade; and the cosmetic seed catalogue embedded in `OnModelCreating`. Password and external account creation also duplicate default-role, Capy-provisioning, and confirmation-email onboarding.

The service layer is generally more cohesive than file sizes initially suggest. Progression, CoFID, USDA mapping, historical snapshotting, calculations, provisioning, and saved-meal operations have clear responsibilities and strong tests. Likewise, frequent explicit ownership predicates are a security strength rather than repository-pattern duplication. No material N+1 query was identified; recent/frequent-food scans are bounded, category/server lists are paged where appropriate, and the intentional client-side Customisation catalogue remains small.

This report records **10 Priority A candidates**, **8 Priority B candidates**, and **10 explicit Priority C leave-alone decisions**. The recommended first refactor is the pure food-validation boundary (A8), preceded or accompanied by a CI verification gate (A10). It is small enough to validate the approach while removing a concrete dependency-direction problem. No application code, tests, migrations, or assets were changed by this audit.

## 2. Current architecture

The application is a single ASP.NET Core 10 process deployed as a systemd service on a Linux VM behind Caddy. `Program.cs` configures Razor Pages, controller endpoints, ASP.NET Core Identity, EF Core/SQLite, rate limiting, forwarded headers, Data Protection, security headers, and the application's scoped/singleton services. Production schema migration is deliberately external to application startup.

The primary UI is server-rendered Razor Pages. A React/Vite island is mounted by the authenticated food-search Razor Page and calls the cookie-authenticated, antiforgery-protected `/api/foods` controller. Food catalogue providers expose the packaged CoFID 2021 resource and the remote USDA FoodData Central service through a common catalogue boundary. Most feature PageModels use `ApplicationDbContext` directly, sometimes alongside focused services.

Identity uses EF Core stores in the same SQLite database as diary, food, community, progression, and Capy data. Resend is wrapped by the application's `IEmailSender` implementation for account and feedback email. Community-food moderation, Capy provisioning/customisation, saved outfits, daily activity, XP, streaks, and achievements remain in-process modules rather than separate applications.

### Component boundaries discovered

| Boundary | Current responsibility and relationships |
| --- | --- |
| Browser | Receives server-rendered HTML/CSS/vanilla JS. Identity cookies authenticate requests; antiforgery protects cookie-authenticated mutations. |
| Reverse proxy/host | Caddy terminates HTTPS and forwards to one ASP.NET Core process supervised by systemd on a Linux VM. Forwarded headers are restricted/configured in the app. |
| Razor Pages | Primary presentation and request orchestration for Dashboard, Diary, Foods/My Foods, Saved Meals, Profile, Progress, Customisation, Community submission/moderation, administration, feedback, and Identity UI. |
| React island | `/Foods/Search` hosts Vite output from `ClientApp`; it performs interactive CoFID/USDA search through `/api/foods` and hands resolved selections back to Razor Diary flows. It is not a general SPA shell. |
| Controller API | `FoodsApiController` exposes authenticated search, suggestions, external-food selection, and favourite mutations. Provider selection is resolved by `FoodCatalogue`; external caching/history policy is delegated to `ExternalFoodResolver`. |
| Application/domain services | Focused services own measurement/calculation, local-time, diary snapshot/copy/maintenance, saved meals, external-food resolution, community workflows, Capy provisioning, and progression/achievement policy. Larger PageModels still contain some candidate rules documented below. |
| Persistence | One Identity-derived `ApplicationDbContext` maps Identity and application tables. EF Core uses SQLite. User ownership is normally filtered in SQL; historical diary/saved-meal nutrition is snapshotted. |
| Food sources | CoFID 2021 is a validated embedded JSON resource loaded in process. USDA FoodData Central is an outbound `HttpClient` integration through an adapter implementing the same provider boundary. |
| Email | ASP.NET Core Identity's `IEmailSender` is implemented via Resend and reused by account/feedback flows. Production validates required configuration without exposing values. |
| Release operations | PowerShell creates a clean self-contained Release package; the Linux extraction script rejects forbidden source/build/database/secret payloads and applies deterministic permissions. Database backup/migration and symlink/service activation are explicit operator steps; the app never migrates at startup. |

### Runtime flow

1. Browser HTTPS traffic reaches Caddy.
2. Caddy forwards trusted proxy metadata and requests to the ASP.NET Core systemd service.
3. The middleware pipeline applies forwarded headers, security headers, exception/status handling, HTTPS/HSTS, routing, Identity authentication/authorization, and endpoint rate limiting.
4. Razor Pages handle the majority of user workflows. The Foods API supports the React search island.
5. PageModels, controllers, and application services use EF Core's `ApplicationDbContext`; Identity uses its EF stores through the same context.
6. EF Core persists to SQLite. CoFID is read from an embedded JSON resource; USDA and Resend are outbound HTTPS integrations.

## 3. Objective codebase observations and metrics

Measurements exclude generated migration designers, publish/build output, the built React bundle, vendored libraries, and the embedded CoFID JSON payload unless stated otherwise:

| Area | Observation |
| --- | --- |
| Authored application files | 232 C#/Razor/JS/JSX/CSS files, approximately 32,659 lines |
| Razor PageModel files | 28 under `Pages` plus 31 scaffolded/customized Identity Area PageModels |
| Application service files | 32 under `CalorieTracker/Services` |
| Checked-in migrations | 41 migration classes, excluding designers and the model snapshot |
| Direct `ApplicationDbContext` consumers | 34 PageModel/controller/service/security boundary files |
| Test source | 68 authored C# files, 18,221 lines, 509 `[Fact]`/`[Theory]` methods producing 713 discovered cases |
| Frontend tests | 8 Node test files, 41 passing test cases |
| Authored image assets | 61 outside vendored libraries; one unreferenced candidate identified |
| Largest stylesheet | `wwwroot/css/site.css`, 6,210 lines |
| Largest authored React module | `ClientApp/src/App.jsx`, 809 lines |
| Largest application JavaScript module | `wwwroot/js/diary-food.js`, 724 lines |
| Largest service | `ProgressionService.cs`, 706 lines |
| Largest PageModels | `Diary/Create.cshtml.cs` (572), `Diary/Edit.cshtml.cs` (544), `Profile/Index.cshtml.cs` (474), `Customisation.cshtml.cs` (402), `Foods/Index.cshtml.cs` (361) |
| Other large boundary classes | `FoodsApiController.cs` (457), `UsdaFoodService.cs` (501), `CofidFoodCatalogueProvider.cs` (459), `ApplicationDbContext.cs` (756) |

File size is evidence for inspection, not itself a finding. Generated migrations and coherent catalogue/provider implementations will be judged separately from presentation classes that mix orchestration and domain rules.

## 4. Priority A — worth doing now

### A1. Extract the duplicated Diary measurement workflow

- **Finding:** `Diary/Create` and `Diary/Edit` independently implement the same exact/portion/approximate measurement validation and calorie-resolution workflow.
- **Evidence:** `Pages/Diary/Create.cshtml.cs` is 572 lines; `OnGetAsync` spans roughly lines 84–207 and `OnPostAsync` roughly lines 251–517. `Pages/Diary/Edit.cshtml.cs` is 544 lines; `OnPostAsync` spans roughly lines 137–502. Both handlers validate measurement modes, food ownership, serving-unit and portion identifiers, quantity bounds, conversion availability, overflow, and `ModelState` errors. Edit adds legitimate historical-food and snapshot-preservation exceptions.
- **Why it matters:** Fixes to serving modes or validation can land in one flow but not the other.
- **Smallest safe change:** Introduce one focused diary measurement resolver that accepts submitted measurement fields plus an explicit create/edit context, and returns canonical values or field-keyed errors. Keep authorization, persistence, snapshots, redirects, and edit-only historical policy in the PageModels.
- **Risk:** Medium; edit deliberately accepts some deleted/inactive historical records.
- **Expected payoff:** High: one tested rule source and substantially shorter handlers.
- **Tests required:** First add a shared matrix for all three modes, invalid food/portion ownership, deleted historical food/portion behavior on edit, boundary quantities, missing conversions, and overflow. Retain `DiaryPageModelTests`, `DiaryEditPrepopulationTests`, `UsdaVolumeLoggingTests`, `DailyMaintenanceSnapshotTests`, and `DiaryEntrySnapshotTests`.
- **Dependencies/sequence:** Tests first; resolver second; migrate Create; migrate Edit last.

### A2. Separate EF model configuration from the cosmetic seed catalogue

- **Finding:** `ApplicationDbContext.OnModelCreating` mixes application-wide relational mapping with a long, frequently expanded cosmetic catalogue.
- **Evidence:** `Data/ApplicationDbContext.cs` is 756 lines. Approximately lines 1–337 configure relationships, indexes, delete behavior, constraints, and defaults for about 17 `DbSet`s; lines 338–755 contain 51 stable `CapyItem` seed definitions.
- **Why it matters:** Catalogue additions make a schema-critical file hard to review, and accidental seed metadata changes can generate destructive/noisy migrations.
- **Smallest safe change:** Move the exact existing `CapyItem` objects, unchanged, into `CapyItemCatalogue.Items` and pass that collection to `HasData`. Moving entity mappings to `IEntityTypeConfiguration<T>` can be a later, separate package.
- **Risk:** Medium because EF snapshots are sensitive to semantically equivalent seed changes.
- **Expected payoff:** High change safety/reviewability; moderate size reduction.
- **Tests required:** Assert stable IDs and essential metadata, run migration/fresh-database tests, and perform a model-difference check that must be empty. Do not add a migration if the model is unchanged.
- **Dependencies/sequence:** Preserve exact seed values first; configuration extraction later.

### A3. Consolidate chronological CSS override layers incrementally

- **Finding:** The global stylesheet contains several generations of page styling and later override blocks for the same components.
- **Evidence:** `wwwroot/css/site.css` is 6,210 lines. Major sections begin around My Foods line 139, Diary 726, initial Capy 1,380, theme 1,675, “CAPY CUSTOMISATION REDESIGN” 1,742, dashboard 2,501, Progress 3,135, logged-out home 3,520, design system 3,728, shell 3,962, scenic headers 4,282, “PHASE 2 DASHBOARD” 4,519, and Profile 5,524. Selectors such as `.capy-item-tile`, `.capy-customisation-layout`, `.dashboard-main-card`, and `.dashboard-macro-grid` recur; some are valid responsive variants, others later-generation overrides.
- **Why it matters:** Cascade order rather than clear component boundaries determines behavior, increasing the regression risk of small UI changes.
- **Smallest safe change:** Inventory selectors against rendered pages, remove only rules proven unreachable or fully superseded, then group survivors into a few logical files (foundation/shell, scenic header, dashboard, diary/foods, progression/profile, customisation). Preserve load order and computed values; do not redesign.
- **Risk:** Medium-high because existing CSS tests primarily verify tokens/selectors rather than computed layout.
- **Expected payoff:** High maintainability and safer UI work.
- **Tests required:** Targeted DOM/style smoke checks where feasible and a desktop/mobile, light/dark visual baseline for major pages. Compare each page family after each small extraction.
- **Dependencies/sequence:** Selector inventory/screenshots first; one page family per PR; shared foundation last.

### A4. Give Profile an explicit form-input and validation boundary

- **Finding:** The Profile PageModel combines HTTP binding, unit conversion, cross-field validation, goal/calorie policy, persistence mapping, snapshots, progression hooks, and theme updates.
- **Evidence:** `Pages/Profile/Index.cshtml.cs` is 474 lines with six dependencies. It binds the EF `UserProfile` entity directly and implements `ValidateBasicProfileFields`, `ApplyImperialConversions`, `ValidateGoalFields`, `ApplyCalorieTargetMode`, `ValidateCalculatedTarget`, entity updates, and persistence. `[BindNever]` protects identity fields but presentation and persistence remain coupled.
- **Why it matters:** Cross-field rules and conversions are difficult to test without constructing a PageModel/`ModelState`; persistence and UI evolution are coupled.
- **Smallest safe change:** Add a page-specific input model and a focused processor/mapper that produces canonical metric values plus field-keyed errors. Keep redirects, user lookup, save, snapshots, progression, and theme endpoints in the PageModel. Do not add an application-wide DTO layer.
- **Risk:** Medium; stale-field clearing, target modes, and conversion order are sensitive.
- **Expected payoff:** High testability and clearer handling; moderate size reduction.
- **Tests required:** Add a matrix for future birth dates, invalid option values, metric/imperial boundaries, unit switching, stale-field clearing, custom targets, and the existing-goal exception. Retain calculation/theme tests.
- **Dependencies/sequence:** Input-model tests, then processor, then Razor binding.

### A5. Move wardrobe invariants out of the Customisation PageModel

- **Finding:** The PageModel is both page composer and authority for equipping, ownership/slot rules, and saved-outfit mutation.
- **Evidence:** `Pages/Customisation.cshtml.cs` is 402 lines. Its handlers provision, equip/unequip, unlock secret items, save/restore/delete outfits, validate names, and shape page collections. `CapyProvisioningService` already owns the narrower idempotent starter/default setup.
- **Why it matters:** Direct equips and saved-outfit restores must enforce the same invariants but are hard to test independently.
- **Smallest safe change:** Add a focused `CapyWardrobeService` for ownership/category validation, single-slot equip/unequip, and outfit capture/apply/delete. Leave page filtering/view shaping in the PageModel and provisioning in its current service.
- **Risk:** Medium; null slots and inactive/unowned items require exact behavior preservation.
- **Expected payoff:** Moderate-high clarity and direct operation tests.
- **Tests required:** Add invalid category, inactive/unowned outfit slots, partial outfits, cross-user IDs, repeated apply/delete, and “No hat” cases. Retain `CapyTests` and `CapyNameTests` migration round-trip coverage.
- **Dependencies/sequence:** Tests; single-item path; saved-outfit path.

### A6. Decompose the React food-search component without expanding React

- **Finding:** The food-search island is appropriately isolated, but nearly all state, requests, provider switching, favourites, pagination, autocomplete, and rendering live in one component.
- **Evidence:** `ClientApp/src/App.jsx` is 809 lines and owns roughly 15 state values plus the full render tree. Pure latest-request, autocomplete, and provider-switch helpers already provide incremental seams.
- **Why it matters:** Async races and state transitions are difficult to reason about, and small changes touch a large component.
- **Smallest safe change:** Extract an API client and one `useFoodSearch` hook, then split stable visual units such as provider controls, results, and pagination. Keep the island on `/Foods/Search`; do not convert Razor Pages or add global state.
- **Risk:** Medium due abort/latest-request, keyboard, and selection behavior.
- **Expected payoff:** Moderate-high maintainability without architecture expansion.
- **Tests required:** Add interaction tests for rapid query replacement, provider changes, pagination, selection, and favourite failure before extraction.
- **Dependencies/sequence:** Tests/API client; hook; visual components.

### A7. Extract testable state transitions from the Diary browser script

- **Finding:** The Diary measurement UI is one DOM-heavy script whose calculations are better tested than interactions.
- **Evidence:** `wwwroot/js/diary-food.js` is 724 lines. One IIFE owns suggestion fetching/rendering, ARIA/keyboard behavior, selection, three measurement modes, edit restoration, units, and summaries. `ClientApp/src/diaryMeasures.test.js` executes the shipped script in a VM but mostly covers calculation/display helpers.
- **Why it matters:** Interaction regressions can occur even while calculation tests pass.
- **Smallest safe change:** Extract pure submitted-state-to-view-state and portion-summary helpers into the existing testable JS boundary, then isolate autocomplete DOM orchestration. Keep vanilla JavaScript and Razor markup.
- **Risk:** Medium due accessibility, keyboard, and edit prepopulation.
- **Expected payoff:** High confidence; moderate complexity reduction.
- **Tests required:** Keyboard navigation, escape/blur, all mode transitions, custom serving text, and historical edit initialization.
- **Dependencies/sequence:** Coordinate concepts with A1, but keep client/server refactors in separate PRs.

### A8. Remove MVC `ModelState` from reusable food validation

- **Finding:** The shared food validator is implemented in terms of Razor/MVC presentation state and mutates the EF entity while validating it; a non-presentation service constructs a fake `ModelStateDictionary` to reuse it.
- **Evidence:** `Services/ValidationRules.cs` is 197 lines and `ValidateFood` accepts `Food`, `ModelStateDictionary`, a UI field prefix, and an `out` dimension. `Pages/Foods/Create.cshtml.cs` and `Pages/Foods/Edit.cshtml.cs` call it directly. `Services/CommunityFoodService.cs` imports `Microsoft.AspNetCore.Mvc.ModelBinding`, creates a `ModelStateDictionary`, and discards it merely to decide whether a food may be submitted.
- **Why it matters:** A business validation rule depends on the web presentation layer, cannot be tested without MVC types, and has a non-obvious normalization side effect.
- **Smallest safe change:** Introduce a pure food validation/normalization result containing normalized values, measurement dimension, and field-keyed errors. Add a tiny PageModel adapter that copies errors into `ModelState`. Have community submission inspect the same pure result. Keep date/meal/binding helpers local or in the existing class; do not build a generic validation framework.
- **Risk:** Low-medium because current normalization order and exact error keys/messages are part of rendered behavior.
- **Expected payoff:** Moderate-high: a clean reusable boundary and safer food/community changes.
- **Tests required:** Direct tests for all name, nutrient, basis, label, unit, overflow, trimming, and canonical-size cases; retain rendered PageModel validation tests and community submission tests.
- **Dependencies/sequence:** This is the best low-scope first package. It is independent of A1, though the result pattern can inform the Diary resolver.

### A9. Centralize and behavior-test new-account onboarding

- **Finding:** Password and external registration duplicate the high-consequence post-creation sequence: assign Standard role, provision the Capy, generate/encode a confirmation token, construct the same email, and send it.
- **Evidence:** `Areas/Identity/Pages/Account/Register.cshtml.cs` is 235 lines and repeats the sequence around lines 143–184; `ExternalLogin.cshtml.cs` is 249 lines and repeats it around lines 170–211. `CommunityFeatureTests.ExternalRegistrationCreation_AssignsStandardAfterIdentityLoginIsCreated` verifies ordering by reading the C# source and comparing string offsets instead of exercising the external-registration behavior.
- **Why it matters:** The two entry paths can drift in default authorization, starter ownership, or email content. The source-text test makes safe internal movement fail while not proving runtime behavior.
- **Smallest safe change:** First replace the ordering source assertion with an executable external-login onboarding test. Then extract only the common post-user/post-login onboarding operation and confirmation-email construction; retain password creation, external provider callback/linking, sign-in, and redirect decisions in their Identity PageModels. Preserve existing failure behavior unless a separately reviewed reliability change is requested.
- **Risk:** Medium because registration, roles, email confirmation, and external-login order are security-sensitive.
- **Expected payoff:** High consistency for account initialization; moderate line reduction.
- **Tests required:** Password and external creation each receive exactly Standard, starter Capy ownership/appearance, one valid confirmation email, and no privileged posted role; duplicate/failed Identity outcomes must not call onboarding. Keep current branded-email and role tests.
- **Dependencies/sequence:** Behavior test first. Do not combine with Identity UI restyling or failure-policy changes.

### A10. Put the existing verification suite behind a CI gate

- **Finding:** The repository has a strong automated suite and deterministic release scripts but no checked-in CI workflow to run them on changes.
- **Evidence:** No files exist under `.github/workflows`. `CalorieTracker.Tests` currently discovers 713 passing .NET cases; `ClientApp` has `test`, `lint`, and `build` scripts; `scripts/Publish-Release.ps1` and `scripts/extract-release.sh` provide release safeguards but are invoked outside a repository CI gate.
- **Why it matters:** The intended “713 before → 713+ after” refactor contract depends on every contributor remembering all server and frontend checks. Seed/migration and built-island drift are especially easy to miss.
- **Smallest safe change:** Add one pull-request/push workflow that restores/builds .NET, runs the Release test suite, runs `npm ci`, frontend tests and lint, builds the Vite island, and fails if generated checked-in output differs. Keep production deployment manual and separate.
- **Risk:** Low; initial runner/version or generated-output differences may need one setup iteration.
- **Expected payoff:** High change safety for every subsequent package.
- **Tests required:** The workflow is the guard; validate it on a documentation/test branch without adding deployment credentials.
- **Dependencies/sequence:** Ideally first or alongside A8, before structural work.

## 5. Priority B — beneficial later

### B1. Decide the legacy USDA detail route using production evidence

- **Finding:** `Pages/Foods/ApiFood` duplicates part of the external-food selection/favourite path now served by the React UI and `/api/foods`, but remains supported.
- **Evidence:** `Pages/Foods/ApiFood.cshtml.cs` is 259 lines and USDA-specific. Current navigation tests assert the new page does not link to it, yet `Program.cs` retains route-specific policy and `ApiFoodPageModelTests` protect it.
- **Why it matters:** Two paths can drift, but removal could break bookmarks/external links.
- **Smallest safe change:** Review non-sensitive route telemetry/logs and document compatibility intent, then deliberately retain it or deprecate through a tested redirect/handoff.
- **Risk:** Medium due unknown callers.
- **Expected payoff:** Moderate if retired; low if it is a needed compatibility route.
- **Tests required:** Route/redirect, validation, cache, and favourite behavior.
- **Dependencies/sequence:** Usage evidence before code change.

### B2. Consolidate favourite mutations after food flows stabilize

- **Finding:** Ownership-scoped favourite mutations appear in the Foods Razor Page, API controller, and legacy API-food PageModel.
- **Evidence:** `Pages/Foods/Index.cshtml.cs`, `Controllers/FoodsApiController.cs`, and `Pages/Foods/ApiFood.cshtml.cs` each query/mutate `UserFavouriteFood`; external paths also resolve/cache provider foods and handle uniqueness races.
- **Why it matters:** Authorization/idempotency can drift, though flows are not identical.
- **Smallest safe change:** Extract only common add/remove by resolved internal `FoodId`; retain external resolution and HTTP shaping in callers.
- **Risk:** Low-medium.
- **Expected payoff:** Moderate consistency.
- **Tests required:** Idempotency, cross-user isolation, nonexistent foods, and cache races.
- **Dependencies/sequence:** Prefer after B1.

### B3. Split progression only when the achievement catalogue grows

- **Finding:** `ProgressionService` is large but remains a coherent transaction boundary with unusually strong coverage.
- **Evidence:** `Services/ProgressionService.cs` is 706 lines and contains reconciliation, per-domain qualification queries, idempotent awards/events, activity recording, and summary projection. Dedicated progression suites cover service, hooks, filters, reconciliation, page, persistence, and streaks.
- **Why it matters:** Future achievement growth may impair navigation, but splitting now would mostly move code and could obscure transaction guarantees.
- **Smallest safe change:** When growth warrants it, extract read-only domain qualification evaluators while retaining one award-ledger orchestrator for transactions/idempotency.
- **Risk:** High if premature.
- **Expected payoff:** Moderate later; low now.
- **Tests required:** Preserve concurrency, reconciliation, idempotency, filter, and persistence coverage; add evaluator contracts.
- **Dependencies/sequence:** Trigger on catalogue growth/new sources, not line count.

### B4. Extract startup registration groups only when they gain policy

- **Finding:** `Program.cs` is long but remains a readable composition root.
- **Evidence:** At 335 lines it visibly groups auth, production validation, rate limiting, EF/Identity, HTTP clients, application services, operator commands, and middleware.
- **Why it matters:** Further growth may add noise, but arbitrary extension methods merely hide configuration.
- **Smallest safe change:** If needed, extract one named rate-limit setup and one application-service registration method; retain middleware order/environment branches in `Program.cs`.
- **Risk:** Low; payoff is also low.
- **Expected payoff:** Modest readability.
- **Tests required:** Production configuration, routing, rate-limit, and startup integration tests.
- **Dependencies/sequence:** After request-path work.

### B5. Propagate request cancellation through remote food lookups

- **Finding:** The catalogue interfaces and USDA client do not accept a `CancellationToken`, so disconnecting/cancelling a search cannot cancel provider work promptly.
- **Evidence:** `Services/IFoodSearchService.cs` and `IFoodCatalogueProvider` in `Services/FoodCatalogue.cs` expose tokenless `Search...`/`ResolveAsync` methods. `UsdaFoodService` calls `PostAsJsonAsync`, `ReadFromJsonAsync`, and `GetFromJsonAsync` without tokens. `FoodsApiController` and `ExternalFoodResolver` consequently cannot pass `HttpContext.RequestAborted` through the provider boundary.
- **Why it matters:** Slow/abandoned USDA requests can consume outbound connections and server work until timeout. This is reliability hygiene, not currently evidenced as a production incident.
- **Smallest safe change:** Add optional tokens through the catalogue interfaces/adapters/resolver and pass `RequestAborted` from API actions. Preserve timeout/unavailable mapping, while rethrowing cancellation caused by the caller rather than converting it to a 503.
- **Risk:** Low-medium due interface/test-fake churn and cancellation exception semantics.
- **Expected payoff:** Moderate under slow or cancelled external requests; small in normal operation.
- **Tests required:** Caller cancellation, provider timeout/unavailability, and cached fallback behavior.
- **Dependencies/sequence:** Do after A6 or alongside its API-client work, but in a separate server PR.

### B6. Remove only the two currently verified orphaned static artifacts

- **Finding:** One JavaScript preference file and one superseded hero image have no application or documentation references.
- **Evidence:** `wwwroot/js/macro-goals-preference.js` looks for `data-macro-goals-toggle`, `data-macro-goals-content`, and `data-macro-goals-compact`; those attributes and the script filename occur nowhere else in authored source. `wwwroot/images/brand/dashboard-hero-capy1.png` is tracked but has no filename reference; every scenic header uses `dashboard-hero-capy.png`. An inventory of 61 authored image assets found only that image with zero filename references.
- **Why it matters:** They add minor ambiguity and package weight (the image is 919,074 bytes), but do not affect runtime behavior.
- **Smallest safe change:** In a dedicated cleanup commit, repeat the reference and publish-output check, then delete exactly these two files.
- **Risk:** Low, subject to checking that no external document hotlinks the old image path.
- **Expected payoff:** Small.
- **Tests required:** Build/publish, static asset smoke checks, and the existing dashboard/scenic-header tests.
- **Dependencies/sequence:** Independent; may accompany a documented asset inventory, not CSS behavior changes.

### B7. Replace source-text tests when their feature is next changed

- **Finding:** A small number of tests assert implementation source strings rather than executable behavior.
- **Evidence:** `CommunityFeatureTests.ExternalRegistrationCreation_AssignsStandardAfterIdentityLoginIsCreated` reads `ExternalLogin.cshtml.cs` and compares string positions. Several `FoodSearchDiscoverabilityTests` read `App.jsx` or shipped scripts and assert exact source fragments. The repository also has capable `WebApplicationFactory` and Node test harnesses, so some of these assertions can move to behavioral coverage.
- **Why it matters:** These tests impede internal movement while sometimes failing to prove the user-visible or security outcome.
- **Smallest safe change:** Replace assertions opportunistically when touching the relevant flow: external onboarding with an executable Identity test; React state/controls with component or DOM interaction tests. Retain source checks that intentionally enforce packaging/no-CDN policy where runtime inspection is impractical.
- **Risk:** Low if replacement tests exist before deletion.
- **Expected payoff:** Moderate refactor freedom and stronger evidence.
- **Tests required:** The replacement behavior test is itself the prerequisite; never simply remove a guard.
- **Dependencies/sequence:** A9 needs the Identity replacement first; A6 should replace relevant React string tests during its test-first stage.

### B8. Replace the bounded Admin Users role N+1 query

- **Finding:** The admin user list loads up to 30 users and then asks Identity for each user's roles individually.
- **Evidence:** `Pages/Admin/Users.cshtml.cs` queries `db.Users...Take(30).ToListAsync(ct)` and then loops through `matches`, awaiting `users.GetRolesAsync(user)` once per row. That is up to 31 database queries for one page load.
- **Why it matters:** This is a genuine N+1 pattern, but it is bounded and restricted to a low-traffic administration page, so it is not a production hot-path emergency.
- **Smallest safe change:** Load the 30 users first, then fetch their role memberships/role names in one ownership-independent join query and group in memory for the view. Preserve current ordering and authorization.
- **Risk:** Low.
- **Expected payoff:** Small-moderate: predictable two-query behavior and simpler performance characteristics.
- **Tests required:** Admin/Owner authorization, filtering/30-row bound, and correct multi-role display/action restrictions.
- **Dependencies/sequence:** Independent and lower priority than the main presentation/domain boundaries.

## 6. Priority C — deliberately leave alone

- **Architecture:** Keep the Razor Pages application with one React island. A SPA rewrite or broader React adoption would add state/deployment complexity without solving an observed problem.
- **Data access:** Do not add generic repositories over EF Core. Explicit ownership predicates and queries are useful; EF already provides unit-of-work/query abstractions.
- **Focused services:** Keep `DailyMaintenanceSnapshotService`, `DiarySnapshotFactory`, `CapyProvisioningService`, `ReusableMealService`, `FoodCatalogue`, and `ExternalFoodResolver` intact; each expresses a coherent policy/provider boundary.
- **Migrations:** Preserve released EF Core history. Do not squash, rename, delete, or “clean up” migrations. Configuration-only refactors must produce no model/schema/seed delta.
- **Platform:** Keep SQLite and the single-process Linux VM/systemd/Caddy deployment until operational evidence requires a migration.
- **Production tooling:** Keep production validation and publish/extraction scripts explicit; replacing them with a new deployment abstraction has no demonstrated payoff.
- **Test database strategy:** Keep the mixed strategy: `TestDatabase.CreateAsync`/`IntegrationTestFactory` use fast in-memory SQLite current-model creation, while dedicated migration suites upgrade released baselines. Converting every test to real migrations would slow the suite without improving every assertion.
- **Ownership predicates:** Keep explicit `UserId == userId` filters close to queries. They occur frequently because ownership is a cross-cutting security invariant; hiding all of them behind a generic repository would reduce review visibility.
- **Read models and calculations:** Keep `CalorieBalanceYearService`, `GoalTimelineCalculator`, `MacroTargetCalculator`, `MeasurementUnits`, `ActivityStreakCalculator`, and the calculation methods on `UserProfile` cohesive. Their domain boundaries and test coverage are clearer than additional service fragmentation.
- **Historical snapshots:** Keep explicit duplicated persisted snapshot fields across diary and saved-meal entities. `DiarySnapshotFactory` centralizes copying; normalizing historical nutrition back to mutable foods would weaken auditability and deletion behavior.

## 7. Test-suite observations

### Current baseline

- `dotnet test CalorieTracker/CalorieTracker.slnx --no-restore --configuration Release` completed with **713 passed, 0 failed, 0 skipped** in this audit.
- `npm test` in `CalorieTracker/ClientApp` completed with **41 passed, 0 failed**.
- The 68 authored C# test files contain 509 test methods and 18,221 lines; theories expand that to the 713 discovered .NET cases.
- Largest concentrations by test-method count are Progression (131), Calculations (85), Diary (63), Foods (59), API (33), Community (33), Capy (21), Database (11), and Identity (10).

### Strengths

- Tests use real SQLite semantics, not EF's non-relational in-memory provider.
- `TestDatabase` centralizes fast current-model setup, users, and disposal. `IntegrationTestFactory` centralizes test authentication, ephemeral Data Protection, fixed time, fake email/USDA services, and an in-memory SQLite connection.
- Dedicated migration tests exercise upgrades from named production baselines, while `ProductionReadinessTests` covers fresh migration/provisioning behavior.
- Ownership, account deletion, historical snapshots, external-food uniqueness races, daily-snapshot races, progression idempotency/reconciliation, role authorization, and failure paths receive meaningful integration coverage.
- Fixed `TimeProvider`/`IUserLocalTimeProvider` test helpers reduce date flakiness.

### Friction and gaps relevant to the plan

- Large PageModels are often instantiated directly. This is useful unit-level coverage, but constructor changes ripple across multiple suites; the proposed extractions should add direct tests for the new focused component rather than introducing a universal PageModel factory.
- The external-registration ordering assertion and several React discoverability assertions read implementation source. B7 describes replacing them only when the corresponding flow is changed.
- CSS/layout coverage is mostly markup or selector-string based. It cannot protect computed cascade, crop, theme, or responsive layout; A3 requires a repeatable manual/visual baseline before deletion or reordering.
- React helper tests cover request ordering and provider switching, but the 809-line component has limited end-to-end interaction coverage. A6 requires tests around rapid queries, provider switches, favourites, pagination, and selection.
- The Diary script has good pure calculation examples but weaker keyboard/DOM/mode-transition coverage. A7 identifies the missing cases.
- Registration has a real password-path integration test but external new-account onboarding is not exercised behaviorally end to end.
- `IntegrationTestFactory` uses `EnsureCreated` by design; it must not be treated as migration validation. Continue using the dedicated baseline-upgrade suites for that purpose.

## 8. Proposed staged refactoring plan

Each package is intended to be independently mergeable, reviewable, testable, and revertible. None requires a schema change.

| Package | Scope and files | Prerequisite protection | Behavior impact | Risk / complexity | Dependencies and completion criteria |
| --- | --- | --- | --- | --- | --- |
| 1. Pure food validation boundary | `ValidationRules`, Food Create/Edit PageModels, `CommunityFoodService`, focused tests | Add direct normalization/error matrix and retain rendered/community tests | None intended | Low-medium / Small-medium | Independent. Complete when reusable validation has no MVC dependency and messages/normalized persisted values remain identical. |
| 2. Account onboarding seam | Register and ExternalLogin PageModels, a focused onboarding/email component, Identity tests | Replace source-order assertion with an executable external-registration test | None intended | Medium / Medium | Package 1 is not required. Complete when both paths share defaults/email construction and all Identity/role/Capy tests pass. |
| 3. Diary measurement resolver | Diary Create/Edit PageModels plus one focused resolver/result | Add create/edit matrix, especially historical deleted food/portion differences | None intended | Medium / Large | Use the simple result style learned in package 1. Complete when both handlers delegate measurement resolution and preserve snapshots/errors/redirects. |
| 4. Profile input processor | Profile PageModel/Razor binding, page input model, pure conversion/validation processor | Expand boundary and mode-switch matrix | None intended | Medium / Medium | Independent after CI. Complete when EF entity is no longer bound directly and all calculations/persistence are unchanged. |
| 5. Wardrobe operations service | Customisation PageModel, focused wardrobe service/tests | Add inactive/unowned/partial/cross-user saved-outfit cases | None intended | Medium / Medium | Independent. Complete when equip/outfit invariants are directly tested and the PageModel remains responsible only for HTTP/view composition. |
| 6. Cosmetic seed isolation | `ApplicationDbContext`, static catalogue definition, seed/migration tests | Capture all 51 IDs and essential metadata; run empty model-difference check | None; explicitly no migration | Medium / Small-medium | Do after catalogue churn settles. Complete only with zero EF model delta and unchanged fresh/upgraded DB tests. |
| 7. React search decomposition | `ClientApp/src/App.jsx`, API client/hook, a few stable components/tests | Add component/DOM interaction coverage | None intended | Medium / Medium-large | Independent of server changes. Complete when the island remains isolated, generated bundle is rebuilt, and request-order behavior is unchanged. |
| 8. Diary JavaScript seams | `wwwroot/js/diary-food.js`, small pure modules, Node tests | Add autocomplete keyboard and state-transition tests | None intended | Medium / Medium | Coordinate vocabulary with package 3 but merge separately. Complete when shipped behavior and Razor integration are unchanged. |
| 9. CSS consolidation by page family | `site.css`, logical stylesheet files, pages/layout only as needed to load them | Desktop/mobile and light/dark visual baseline | None intended | Medium-high / Large, split into sub-PRs | Follow JS/markup stabilization. Complete one page family at a time with matched computed appearance; shared foundation last. |
| 10. Verification and asset hygiene | Add CI workflow; separately remove only the two verified orphaned artifacts | Existing .NET/Node suites, build/publish and static smoke check | None | Low / Small | CI should precede structural packages. Asset deletion is a separate commit and requires a final reference/publish check. |

Later Priority B items should remain separate from these behavior-preserving packages. In particular, deciding the legacy `/Foods/ApiFood` route requires production usage evidence, and favourite/cancellation changes should not be smuggled into UI decomposition.

## 9. Risk areas

- **Released SQLite schemas:** The 41 migrations are immutable operational history. Configuration moves must not produce a model diff. Any future schema work needs fresh-database and released-baseline upgrade tests plus production backup/rollback handling.
- **Historical diary semantics:** Deleted foods/portions, immutable nutrition snapshots, direct-portion foods, mass/volume dimensions, approximate amounts, and maintenance snapshots intentionally make Edit differ from Create.
- **Identity onboarding and deletion:** Role order, confirmation tokens, starter provisioning, external-login linkage, privileged-role restrictions, and transactional account deletion are security/data boundaries.
- **Ownership:** The many explicit `UserId` predicates are intentional. Every extraction must keep ownership filtering in the database query, not materialize first and filter later.
- **Progression concurrency:** XP event uniqueness, reconciliation, daily activity, optional hook failures, and transaction behavior are well tested but high impact. Avoid incidental changes during PageModel cleanup.
- **External-food cache semantics:** The provider result is authoritative when available, the user's cached row is fallback, dimension changes cannot reinterpret history, and unique-insert races are handled explicitly.
- **CSS cascade:** Multiple design generations and responsive overrides make wholesale sorting/deletion unsafe even when selectors appear duplicated.
- **Generated frontend output:** `wwwroot/react-food-search` is deployed output for the Vite island. Source and bundle must change together and CI should detect drift.
- **Local date vs UTC:** User-local diary/activity dates use `IUserLocalTimeProvider`; award/email/storage timestamps often use `TimeProvider`/UTC. Do not replace these with indiscriminate `DateTime.Now` calls.
- **Optional progression hooks:** Domain writes intentionally survive achievement-evaluation failures. Extraction must not accidentally make optional progression part of the primary transaction.

## 10. Suggested first refactor

Start with **Package 1: pure food validation** (A8), ideally after or alongside the CI gate portion of Package 10.

It is smaller and more isolated than the Diary/Profile/Customisation changes, removes a concrete dependency-direction problem (`CommunityFoodService` constructing MVC state), and establishes a useful but non-generic result pattern for later validation extraction. The acceptance bar should be exact parity for error keys/messages and normalized persisted values, direct unit coverage of the pure validator, all 713 .NET and 41 Node tests passing, and no EF model difference.

## 11. Areas inspected

- Repository layout, authored/generated/vendor boundaries, project files, package scripts, and objective file/test counts
- `Program.cs`, `ProductionConfiguration`, middleware order, security headers, forwarded headers, rate limiting, Identity/authorization, DI, operator commands, and the deliberate absence of startup migration
- Caddy/systemd/Linux production shape as backed by README and publish/extraction scripts; package exclusions, persistent data/config, explicit migration, and rollback boundaries
- All normal PageModel areas, with detailed review of Dashboard, Diary, Foods, Profile, Progress, Customisation, Saved Meals, Community/Admin, Feedback, and significant Identity flows
- Foods API, React host/island, catalogue provider boundary, CoFID embedded loader/search, USDA client, external-food resolver/cache, favourites, community-food selection/moderation, and the legacy API-food route
- EF Core context, relationships/delete behavior/indexes/constraints, all 51 cosmetic seeds, 41 migrations at a history level, model snapshot role, and migration/production-readiness tests
- Entities and binding protection, historical nutrition snapshots, measurement/unit helpers, goal/macro/calorie calculations, user-local time, daily maintenance snapshots, diary copy, and reusable meals
- Progression service, activity filter/hooks, definitions, XP/achievement/streak/reconciliation responsibilities and their dedicated suites
- Capy provisioning, catalogue categories, ownership, equip slots, avatar layering path, saved outfits, client pagination/equip script, and tests
- Resend adapter, account confirmation and feedback email paths
- Global CSS organization and repeated design-generation blocks; all authored JavaScript file sizes/references; React state/helper/test organization
- Test support, direct PageModel tests, WebApplicationFactory integration tests, SQLite setup, migration baseline tests, fakes/fixed clocks, test distribution, and source-text assertions
- Conservative dead-code/static-asset reference scan, including legacy recipe migrations and the `/Foods/ApiFood` compatibility route
- Query patterns for ownership, bounded recent/frequent food materialization, pagination, tracking, yearly balance, and obvious N+1 loops; the only clear instance found is the bounded Admin Users role lookup documented in B8

## 12. Areas not fully inspected

- Production traffic/log telemetry was not available, so `/Foods/ApiFood` usage and the practical incidence of slow/cancelled USDA calls remain unknown.
- No load test or database-size/production query-plan capture was performed. Performance conclusions are limited to static query review and existing tests.
- No exhaustive browser-driven computed-style or screenshot comparison was run. The CSS finding is based on stylesheet/markup structure and existing visual-test limitations, not a declaration that every repeated selector is dead.
- Third-party package/library internals were excluded except where application configuration depends on them.
- The contents of the embedded CoFID dataset were not manually reviewed record by record; loader validation, metadata checks, search implementation, and tests were inspected.
- Public production infrastructure secrets, host details, and database contents were intentionally not inspected.
