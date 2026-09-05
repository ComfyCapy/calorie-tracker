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

The application requires these configuration keys. Keep the values in user-secrets or environment variables; do not commit them:

- `ConnectionStrings:DefaultConnection`
- `FoodDataCentral:ApiKey`
- `Resend:ApiKey`

For local development, the React food-search island can be rebuilt with:

```bash
cd CalorieTracker/ClientApp
npm install
npm run build
```

The build writes the production island assets to `CalorieTracker/wwwroot/react-food-search`, which are currently committed because the .NET project does not build the React island automatically.
