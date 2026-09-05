# CalorieTracker

A calorie and nutrition tracking web app I'm building with ASP.NET Core.

The main idea is to make food logging less tedious while still giving users control over how precise they want to be. Foods can be logged using exact quantities or more natural portions such as `1 banana`, `2 slices` or `1 bowl`.

The project is still in active development and hasn't reached MVP yet.

## Current features

- User registration and login
- Unique username-based accounts
- User-specific food diaries
- Breakfast, lunch, dinner and snack sections
- Add, edit and delete diary entries
- Daily calorie and macro totals
- Date navigation
- Custom food creation and management
- External food database search using USDA FoodData Central
- Importing and logging USDA foods
- Favourite database foods
- Recently logged database foods
- Saved custom portion sizes
- Exact and portion-based food logging
- Portion information preserved in diary entries
- Soft deletion of custom foods to preserve diary history
- Basic user profiles and calorie targets
- Light, dark and system themes

## Food database

CalorieTracker integrates with the USDA FoodData Central API to provide access to an external nutrition database.

Users can search for foods, view their nutritional information and add them directly to their diary.

Database foods that are used by a user are stored locally with their external USDA identifier and source information. This allows them to be reused for features such as favourites and recently logged foods without treating them as user-created custom foods.

The food library is currently divided into:

- Favourites
- Custom Foods
- Recent Foods

This keeps personally created foods separate from foods sourced from the external database while still making commonly used foods easy to access.

## Portion system

One of the main goals of the project is to make food logging flexible without forcing everything to be weighed precisely.

Custom foods can have multiple saved portions. For example:

- 1 banana
- 1 slice
- 1 medium bowl
- 1 cup
- 100 g

When adding a diary entry, foods with saved portions can be logged either using an exact quantity or one of their portions.

For example, a portion could define:

`1 slice = 35 g`

Logging `2 slices` would therefore be stored as `70 g`, allowing the existing calorie and macronutrient calculations to continue working while the diary can still display the more useful `2 × slice` description.

Foods without saved portions simply use exact quantity logging without displaying unnecessary portion controls.

## Next planned work

- Recipes and saved meals
- Weight tracking
- Mobile responsiveness
- Additional tests and error handling

## Tech

- C#
- ASP.NET Core
- Razor Pages
- Entity Framework Core
- ASP.NET Core Identity
- SQLite
- USDA FoodData Central API
- HTML/CSS/JavaScript
- Bootstrap

## Running locally

You'll need the .NET SDK installed.

Clone the repository:

```bash
git clone https://github.com/SertraLDN/calorie-tracker
cd calorie-tracker
```

Restore dependencies:

```bash
dotnet restore CalorieTracker/CalorieTracker.csproj
```

Apply database migrations:

```bash
dotnet ef database update --project CalorieTracker/CalorieTracker.csproj
```

Run the application:

```bash
dotnet run --project CalorieTracker/CalorieTracker.csproj
```

Run the automated test suite:

```bash
dotnet test CalorieTracker/CalorieTracker.slnx
```

The remaining browser and release checks are listed in
[`MANUAL-TESTING.md`](MANUAL-TESTING.md).

The application requires these configuration keys. Keep the values in user-secrets or environment variables; do not commit them:

- `ConnectionStrings:DefaultConnection`
- `FoodDataCentral:ApiKey`
- `Resend:ApiKey`
- `Feedback:RecipientAddress`

`appsettings.Development.json` supplies only the local SQLite connection string. Other environments must provide `ConnectionStrings__DefaultConnection`; startup fails clearly when it is missing rather than creating a database in the application directory.

## Database and first deployment

For the first single-instance portfolio deployment, use SQLite on a persistent mounted volume. It is proportionate to the expected traffic and keeps operation, cost and EF migration compatibility simple. Set `ConnectionStrings__DefaultConnection` to a path on that volume, for example `Data Source=/var/lib/calorietracker/calorietracker.db`. Do not use an application/deployment directory that is replaced or discarded during releases.

If the chosen host cannot provide persistent storage, or the application later needs multiple instances or materially more concurrent writes, move to managed PostgreSQL. That change is intentionally deferred until the deployment platform is selected because it requires the Npgsql provider, provider-specific migration verification and a planned data transfer; the repository does not maintain two migration histories today.

Apply migrations as an explicit release/pre-deploy step after taking a backup and before starting the new application version:

```bash
dotnet ef database update --project CalorieTracker/CalorieTracker.csproj
```

The release environment must supply the production connection string to that command. The application deliberately does not run `Database.Migrate()` at startup, avoiding hidden migration failures and multi-instance migration races.

To prove a new installation independently of any development database, choose an unused disposable path and run:

```bash
dotnet ef database update --project CalorieTracker/CalorieTracker.csproj --connection "Data Source=/tmp/calorietracker-fresh.db"
```

The migration chain creates the complete schema and system Capy catalog. Runtime provisioning gives each new user the active starter inventory and equips the default expression/background; it does not grant the Gold Crown.

Back up the SQLite database daily, retaining at least seven daily copies and several weekly copies for a small deployment. Prefer a platform volume snapshot or SQLite's online backup mechanism/`sqlite3 .backup`; do not copy a live database file while writes may be in progress. To restore, stop application writes, restore the database to a replacement persistent path, point the connection string at it, run the checked-in migrations, and then start the app. Keep source/migrations and deployment configuration in version control, but store connection strings, API/email credentials and persistent ASP.NET Core data-protection keys separately in the platform's secret/persistent-storage facilities.

Diary entries snapshot food name, serving basis/unit, nutrition and portion name/count at logging time. Later custom-food or portion edits and soft deletion do not rewrite history. Existing cached USDA foods may be reused if upstream data disappears, while new external foods require a successful server-side fetch; previously logged diary nutrition never depends on a later USDA response.

For local development, the React food-search island can be rebuilt with:

```bash
cd CalorieTracker/ClientApp
npm install
npm run build
```

The build writes the production island assets to `CalorieTracker/wwwroot/react-food-search`, which are currently committed because the .NET project does not build the React island automatically.
