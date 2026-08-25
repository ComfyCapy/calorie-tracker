# CalorieTracker

A calorie and nutrition tracking web app I'm building with ASP.NET Core.

The main idea is to make food logging a bit less tedious, particularly when it comes to portion sizes. Not everything needs to be weighed to the exact gram, so the plan is to support normal portions like `1 banana`, `2 slices` or `1 bowl`, while still allowing exact weights for people who want them.

The project is still in development and hasn't reached MVP yet.

## Current features

- User registration and login
- Unique username-based accounts
- User-specific food diaries
- Breakfast, lunch, dinner and snack sections
- Add, edit and delete diary entries
- Daily calorie and macro totals
- Date navigation
- Food library with search
- Custom foods
- Basic user profiles and calorie targets
- Light, dark and system themes

## Currently working on

- Finishing user-specific profiles
- BMR/TDEE and calorie target calculations
- Better portion sizes
- Favourite and recent foods
- Improving the food library
- Recipes and saved meals
- Weight tracking
- Replacing the default home page with a dashboard
- Making the UI less Bootstrap-y
- Mobile responsiveness
- Tests and general error handling

## Portion system

This is one of the bigger features I want to build.

Instead of every food being based around entering an exact weight, foods will be able to have multiple useful serving sizes.

For example:

- 1 banana
- 1 slice
- 1 medium bowl
- 1 cup
- 100 g

So if you know something weighs 137 g, you can enter 137 g. If you just ate a banana and don't particularly care whether it was 112 g or 124 g, you can enter a banana.

The idea is to make tracking easier without taking away the option to be precise.

## Tech

- C#
- ASP.NET Core
- Razor Pages
- Entity Framework Core
- ASP.NET Core Identity
- SQLite
- HTML/CSS/JavaScript
- Bootstrap

## Running locally

You'll need the .NET SDK installed.

```bash
git clone https://github.com/SertraLDN/calorie-tracker
cd calorie-tracker
dotnet restore
dotnet ef database update
dotnet run
