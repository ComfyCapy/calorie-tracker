# CoFID catalogue importer

This tool creates the small, normalized CoFID catalogue embedded in Comfy Capy Calories. The source is Public Health England's **McCance and Widdowson's Composition of Foods Integrated Dataset 2021**, published on the [official GOV.UK CoFID page](https://www.gov.uk/government/publications/composition-of-foods-integrated-dataset-cofid).

The official workbook is kept locally at `tools/cofid/source/CoFID_2021.xlsx`. It is intentionally ignored by Git and must not be committed. The generated `CalorieTracker/Data/Catalogues/cofid-2021.json` artifact is the reviewable application input.

## Import rules

- Read worksheet `1.3 Proximates`, with headers on row 1 and data beginning on row 4.
- Import Food Code, Food Name, Description, Group, Protein, Fat, Carbohydrate, and Energy (kcal).
- Convert the CoFID trace marker `Tr` to zero. Treat `N`, blanks, malformed values, and negative core nutrient values as unavailable; exclude any record with an unavailable core nutrient.
- Use a deterministic catalogue ID derived from normalized immutable record content, not workbook row position. Source row remains provenance metadata. Food Code is only one identity component because the workbook contains a duplicated code; truly identical duplicate records receive deterministic occurrence suffixes.
- Map source kcal to the application's integer calorie model using round-to-nearest with midpoint-to-even, matching .NET's default `Math.Round` behavior. Search and persisted foods therefore show the same effective calorie value. Protein, carbohydrate, and fat retain their source decimal precision.
- Store values per 100 g, except records in the verified CoFID alcoholic-beverage groups (`Q`, `QA`, `QC`, `QE`, `QF`, `QG`, `QI`, and `QK`), which use per 100 ml. This follows the workbook's `1.1 Notes` sheet and the source group taxonomy; food-name matching is not used. An unexpected new `Q*` group fails the import for review.
- Sort records by generated ID and serialize JSON with stable key ordering so repeated imports are byte-for-byte deterministic.

## Run

```powershell
python -m pip install -r tools/cofid/requirements.txt
python tools/cofid/import_cofid.py
python -m unittest discover -s tools/cofid/tests -p "test_*.py"
```

The importer prints record counts, exclusions, trace conversions, unavailable `N` values, duplicate source-code counts, and the artifact SHA-256 for review.
