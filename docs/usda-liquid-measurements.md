# USDA liquid measurements investigation

Food-specific fluid-ounce logging is technically sound when the calculation uses an explicit USDA volume portion and its gram weight. USDA's per-100g nutrient basis is not an obstacle. This change adopts option D: make the existing selected-measure workflow read naturally, while retaining its server-side portion calculation. It does not introduce a general density, change a food's nutrient basis, or synthesize additional units.

Selecting `1 fl oz` now displays `Amount (fl oz)`. Entering `12` against a 30g portion previews `12 fl oz (360 g used for nutrition)`. Gram entry remains available. Containers retain their full descriptions and serving counts. This was already possible mathematically through portion multiplication; the improvement is presentation and discoverability, not a new nutrition algorithm.

## 1. Evidence and USDA fields

The investigation inspected `UsdaFoodService`, its inline JSON test fixtures, `Food`, `FoodPortion`, `FoodPortionCandidate`, `ExternalFoodResolver`, `FoodCatalogue`, the search API/React presentation, the portion-management handlers, both Diary handlers and views, the shared Diary script, and snapshot tests. It also inspected USDA's published field documentation and the embedded JSON specification in its official API documentation.

USDA distinguishes fields by dataset. FNDDS puts the household amount and unit in `portionDescription`; its `modifier` is a portion code. SR Legacy uses `amount` with the measure description in `modifier`. Foundation uses `amount`, `measureUnit`, and qualifiers. `gramWeight` describes the mass of the whole portion. A missing or undetermined unit is not evidence of a liquid. [USDA Download Field Descriptions, food_portion, p. 6](https://fdc.nal.usda.gov/docs/Download_Field_Descriptions_Oct2020.pdf)

| Dataset enabled by this app | Evidence for a volume portion | Qualification |
|---|---|---|
| Survey/FNDDS | Household description such as `1 fl oz`, plus positive `gramWeight` | Amount is embedded in the description; do not interpret the modifier's numeric code as an amount. |
| SR Legacy | Numeric `amount`, measure text such as `fl oz` in `modifier`, positive `gramWeight` | Preserve any additional preparation or measure context. |
| Foundation | Numeric amount, named/abbreviated measure unit, positive `gramWeight`, potentially qualifiers | Can support volume where actually supplied; availability is food-specific. |

The application's importer already preserves the chosen description and gram weight as a named portion. It excludes invalid weights and unusable labels, and skips FNDDS's unspecified-quantity portion. These import rules were not changed.

The API's `FoodPortion` schema exposes amount, gram weight, description, modifier and measure-unit information. The inspected `SearchResultFood` schema does not promise `foodMeasures` or `foodPortions`. The application's search DTO consumes neither, and its result mapping explicitly supplies 100g. Detail DTOs consume `foodPortions`. An optional search field's presence in some responses would need separate completeness/context validation before it could support a presentation policy. [USDA FDC API specification](https://fdc.nal.usda.gov/api-spec/fdc_api.html)

Live response verification was attempted using the public demo key, but USDA returned a rate-limit error. No fresh live beer, milk, or Foundation response was verified. Numerical examples below come from the supplied request, existing fixtures, or explicitly synthetic regression fixtures; they are not presented as a survey of current USDA records. The official schema and field documentation establish the field semantics, not the current prevalence of each measure.

## 2. Food-specific conversion and nutrient authority

For a particular food and measurement context, let a USDA portion contain V fluid ounces and weigh W grams. An entered amount Q in that same unit corresponds to `Q × W / V` grams. The food's nutrient amount is then `nutrient_per_100g × grams / 100`. USDA documents this gram-weight scaling approach for portion nutrients. It does not establish a universal food density. [USDA Foundation Foods documentation, Weights](https://fdc.nal.usda.gov/Foundation_Foods_Documentation/)

For the requested beer example, `1 fl oz = 30g` gives 360g for 12 fl oz. At 43 kcal per 100g, that is 154.8 kcal, approximately 155 when displayed as whole kcal. One fl oz is 12.9 kcal, approximately 13. A separate food whose supplied portion weighs 31g per fl oz yields 372g for 12 fl oz. Using either food's relationship for the other would be incorrect.

These are calculations from authoritative reference measures, not physically exact density measurements for every glass of a product. Sampling, preparation, temperature and source rounding can affect measured values. Keeping the selected source measure explicit makes the calculation auditable.

No `g = ml` assumption is needed or made. The application's global measurement-unit converter already separates mass from volume; this change does not add a mass-to-volume bridge to it.

## 3. Liquid capability and foods that remain mass-based

A food name cannot establish volume capability. Neither a beverage category nor water content would supply a gram-to-volume relationship. A cup or tablespoon can describe flour, chopped vegetables or other solids, so such units do not establish that a food is liquid either.

An explicit fluid-ounce portion is evidence that the source permits that particular fluid-volume measure. A qualified cup, such as a chopped or melted measure, supports only its stated context. A generic liquid capability should not be inferred from the existence of any household measure.

For this implementation, no food receives a liquid classification. USDA foods whose stored selection is exactly `1 fl oz`, `1 cup`, `1 tbsp`, or `1 tsp` receive a clearer quantity label. Matching ignores case and repeated whitespace. It is strictly display shorthand for counting that selected portion, and does not authorize a new conversion. `1 cup, chopped`, `2 tbsp`, container descriptions, and unrecognized labels continue to display their complete portion names.

Foods with no volume portion retain gram entry and any other supplied named portions. This includes Foundation beverages without volume measures, regardless of their names. Solid foods do not gain invented fluid-ounce options. An already supplied solid-food cup remains an explicit cup portion, without implying fluid-ounce support.

## 4. Arbitrary amounts and other US units

Arbitrary positive amounts can safely scale the selected measure within the application's numeric validation. With `1 fl oz` selected, the count is directly the entered fluid-ounce amount. Fractions are supported by the existing 0.01-step UI; server arithmetic uses decimals and rejects overflow. The same selected-measure approach applies to individually supplied `1 cup`, `1 tbsp`, and `1 tsp` portions. A two-tablespoon portion continues to take a number of two-tablespoon servings.

Cross-unit arithmetic is mathematically valid when the source unit and context are unambiguous: one US customary cup is eight US fluid ounces; a tablespoon is half a fluid ounce; three teaspoons make a tablespoon. These volume ratios do not supply food density. They must not be confused with imperial fluid ounces or rounded metric kitchen equivalents. [NIST Metric Household, volume-equivalence table](https://www.nist.gov/pml/owm/metric-household)

A future converter could derive grams per fluid ounce from one trustworthy, context-compatible source measure, then apply those unit ratios. This change deliberately does not generate absent measures, convert a cup-only food into a fluid-ounce food, or convert any USDA food to a 100ml nutrient basis.

## 5. Conflicts and rounding policy

The selected portion always wins for its own calculation. No densities are averaged, no tolerance is used to merge measures, and no canonical measure is chosen over another. For example, twelve 29.7g fluid-ounce portions produce 356.4g; a separately supplied 356g can remains 356g. A materially different 300g can also remains independently selectable and is not silently changed to agree with the ounce portion.

This policy handles both rounding differences and contextual differences without pretending they are the same measurement. Selecting a can explicitly uses the can's supplied weight. Selecting an ounce explicitly uses the ounce's supplied weight.

There is no rounding before server-side portion multiplication. The browser summary uses the existing maximum of four decimal places for readability and is not authoritative. A regression case verifies that `29.72345 × 0.33` stores `9.8087385g` even though the summary shows `9.8087g`. Existing nutrient-storage and display rounding policies remain unchanged.

For a future general converter, reject ambiguous/qualified contexts before comparing numbers. A documented small tolerance might accommodate source rounding, but should not be presented as a USDA rule. Material conflict should disable a general converter and retain explicit named measures. There is insufficient evidence here to justify a universal numeric tolerance across all datasets and portion sizes.

## 6. Architecture and cached-data limitation

The key limitation is provenance, not the per-100g basis. `FoodPortion` persists only its food relationship, name, canonical amount, and deletion flag. The portions page permits the owner to add and edit portions on USDA foods. The resolver intentionally does not overwrite or resurrect existing matching portions. Consequently, a cached portion called `1 fl oz` is not proof of an unchanged USDA measurement.

Promoting arbitrary cached labels into an authoritative food-wide density would be unsafe. That is not what this implementation does: the label controls wording only, and logging still uses the explicitly selected, owned portion as before. An owner-edited measure retains the existing custom-portion semantics; the UI does not newly certify it as USDA-authored.

No model property, migration, persistent conversion cache, new endpoint or extra provider request was added. The existing POST flow remains:

1. Resolve the selected food within the authenticated user's foods.
2. Resolve the selected portion within that food.
3. Multiply the server-loaded portion amount by the entered count.
4. Ignore the posted gram quantity in portion mode.
5. Capture the existing food/portion snapshot when adding an entry.

The API's provider-plus-external-ID resolution, favourites, user isolation and offline cached fallback are unchanged. The browser receives values for its preview but cannot make those values authoritative by posting them back.

A future durable general converter would need trusted structured measure provenance and context, plus invalidation when a portion is edited. Nullable server-owned metadata could support that with a migration. Another possible design would validate the selected food's measurements afresh on the server and retain trusted transient state; that could avoid a schema change but adds API dependence, expiry and offline-fallback concerns. Neither is required for the selected-measure improvement implemented here.

## 7. Diary and historical entries

For USDA foods, the mode label is now `Portion or measure`. Selecting a simple one-unit measure changes the count label to `Amount (fl oz)`, `Amount (cup)`, `Amount (tbsp)` or `Amount (tsp)`. Named cans and bottles continue to use serving counts. The exact/weight route still logs grams.

The edit page now supplies the same USDA presentation flag as the create page and uses the input ID expected by the shared script. Associated quantity labels point to that input.

The investigation also found a preview inconsistency: saving an old portion-based entry already preserves its historical grams-per-portion, but the edit-page JSON used the current portion row's weight. The edit page now derives its preview weight from `entry.Quantity / entry.PortionQuantity`, matching existing save behavior. That value is a non-bound page-model property, not an EF property or a database field. No historical row or nutrition snapshot is rewritten by this change.

CoFID's authoritative 100ml foods retain their existing volume basis and calculations. The shorthand display is gated to USDA foods with gram-based presentation; it is not applied to CoFID or custom foods.

## 8. Search cards

Keep USDA search cards at per 100g. Scaling a card to a trustworthy one-fluid-ounce or can measure would be mathematically valid, but this application's consumed search response does not establish that measure. The published search schema inspected here also does not guarantee the needed measurements and qualifiers. Selecting inconsistent reference measures across search results would further weaken comparisons.

No detail request is issued to decorate a search result. A new regression test supplies multiple results, including optional `foodMeasures` data, and verifies one search request, no detail request, unchanged nutrient values, and 100g cards. Foundation, FNDDS and SR Legacy remain the existing enabled search types; Branded foods were not enabled.

## 9. Exact files changed for this investigation

The workspace already contained substantial uncommitted work. The following list describes only this task's edits; it does not claim ownership of the other working-tree changes.

| File | Change |
|---|---|
| `CalorieTracker/wwwroot/js/diary-food.js` | Selected one-unit USDA measure labels and direct-amount summaries. |
| `CalorieTracker/Pages/Diary/Create.cshtml` | Label IDs for the shared script and explicit quantity-label association. |
| `CalorieTracker/Pages/Diary/Edit.cshtml` | Matching UI hooks, USDA flag, corrected quantity input ID, historical preview amount. |
| `CalorieTracker/Pages/Diary/Edit.cshtml.cs` | Non-bound original portion amount for GET and invalid-POST previews. |
| `CalorieTracker/ClientApp/package.json` | Include Diary script tests in the frontend test command. |
| `CalorieTracker/ClientApp/src/diaryMeasures.test.js` | Eight tests executing the shipped script in a lightweight DOM harness. |
| `CalorieTracker.Tests/Diary/UsdaVolumeLoggingTests.cs` | Eleven theory/fact cases covering import, cache, logging, conflicts, precision and historical edits. |
| `CalorieTracker.Tests/Diary/DiaryEditPrepopulationTests.cs` | Verify the rendered historical portion weight and quantity input association. |
| `CalorieTracker.Tests/Api/UsdaFoodServiceTests.cs` | Multiple-result search test guarding against rebasing and N+1 detail requests. |
| `docs/usda-liquid-measurements.md` | Investigation, design boundary and validation report. |

No USDA importer/resolver, EF entity, nutrient calculation, React component or checked-in generated React asset was changed by this task.

## 10. Tests and validation

| Check | Result |
|---|---|
| Focused .NET suite | 52 passed, 0 failed. |
| Full .NET suite | 417 passed, 0 failed, 0 skipped. |
| Frontend tests | 12 passed, including 8 new Diary tests. |
| Frontend lint | Passed. |
| React production build | Passed; output isolated under `artifacts/usda-liquid/react-build`. |
| Debug application build | Passed, 0 warnings/errors, isolated `artifacts` output. |
| Release application build | Passed, 0 warnings/errors. |
| EF pending-model check | No changes since the last migration. |
| `git diff --check` | Passed. |

Coverage includes FNDDS beer, juice and milk with different weights; SR Legacy amount/modifier measures; Foundation structured measures and existing no-portion fallback tests; food-name independence; no mass/ml substitution; source-weight scaling; named containers; unchanged exact gram logging; forged posted quantity/snapshot values; CoFID 100ml calculations; cached fallback; historical labels and weights; conflict and display-rounding policies; and search request counts. Existing cross-user and cross-food portion rejection tests also pass in the full suite.

The frontend tests run the actual shared script with a lightweight DOM harness. Razor integration tests verify the rendered fields and serialized historical weight. A manual visual browser session was not performed.

The normal Debug output was locked by an already running app, so successful Debug/test runs used isolated output instead of stopping it. An initial isolated location was one directory deeper than two existing source-reading tests expected; moving the output root to `artifacts` resolved those path failures without weakening tests. Restore needed access to the existing NuGet configuration. These environment failures were resolved before the successful results above.

Reproducible commands from the repository root:

```powershell
dotnet test CalorieTracker.Tests/CalorieTracker.Tests.csproj --artifacts-path artifacts --verbosity minimal
dotnet test CalorieTracker.Tests/CalorieTracker.Tests.csproj --no-build --no-restore --artifacts-path artifacts --filter "FullyQualifiedName~UsdaVolumeLoggingTests|FullyQualifiedName~UsdaFoodServiceTests|FullyQualifiedName~ExternalFoodPortionTests|FullyQualifiedName~DiaryPageModelTests|FullyQualifiedName~DiaryEditPrepopulationTests" --verbosity minimal
dotnet build CalorieTracker/CalorieTracker.csproj -c Debug --no-restore --artifacts-path artifacts
dotnet build CalorieTracker/CalorieTracker.csproj -c Release --no-restore
```

Frontend checks ran in `CalorieTracker/ClientApp`: `npm test`, `npm run lint`, and `npm run build -- --outDir ../../artifacts/usda-liquid/react-build`.

The EF check used Release output, the Testing environment, an in-memory SQLite connection and a dummy email-service key: `dotnet ef migrations has-pending-model-changes --project CalorieTracker/CalorieTracker.csproj --configuration Release --no-build`. It did not apply migrations or change a database.

## 11. Limitations and ship recommendation

**Ship recommendation: ship this bounded selected-measure presentation improvement.** Its calculation path is unchanged, the additional history preview matches existing server behavior, and all required automated checks pass. This is not a recommendation to ship all unrelated changes already present in the working tree.

Do not describe this as a general g/fl oz selector or as newly verified USDA provenance for cached portions. It exposes the already-supported arbitrary amount of an explicitly selected measure more naturally. It does not automatically select a measure, invent units for cup-only foods, normalize spelling variants beyond the four supported one-unit labels, or reconcile conflicts into a single density.

No schema change or migration was required. No commit, push, deployment, or database update was performed.

## Sources

1. USDA FoodData Central. [Download Field Descriptions, October 2020](https://fdc.nal.usda.gov/docs/Download_Field_Descriptions_Oct2020.pdf). Dataset-specific portion fields; pp. 6 and 9. Accessed 10 September 2026.
2. USDA FoodData Central. [FDC Nutrient Data OpenAPI Documentation](https://fdc.nal.usda.gov/api-spec/fdc_api.html). Embedded `FoodPortion`, `MeasureUnit`, and `SearchResultFood` schemas. Accessed 10 September 2026.
3. USDA FoodData Central. [Foundation Foods Documentation](https://fdc.nal.usda.gov/Foundation_Foods_Documentation/). Weights section, portion scaling and dataset-specific weights. Accessed 10 September 2026.
4. NIST. [Metric Household](https://www.nist.gov/pml/owm/metric-household). Household volume ratios; metric equivalents must not be mistaken for exact food mass conversions. Accessed 10 September 2026.
5. Local application and tests listed above, plus `Models/Food.cs`, `Models/FoodPortion.cs`, `Models/DiaryEntry.cs`, `Services/UsdaFoodService.cs`, `Services/ExternalFoodResolver.cs`, `Services/MeasurementUnits.cs`, `Pages/Foods/Portions.cshtml.cs`, and `Pages/Diary/Create.cshtml.cs`. Working-tree implementation inspected 10 September 2026.
