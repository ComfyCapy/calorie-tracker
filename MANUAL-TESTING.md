# CalorieTracker manual release checklist

Use development/test credentials and configuration. Do not use real personal data in screenshots or bug reports.

## Fresh deployment database

- Choose a new disposable SQLite path and confirm the file does not already exist.
- Set the environment's `ConnectionStrings__DefaultConnection` to that path, then run `dotnet ef database update --project CalorieTracker/CalorieTracker.csproj` without copying or editing another database.
- Start the app against the migrated database and register/confirm/sign in with a fresh account.
- Configure Profile, then load Foods, Diary and Comfy Capy Customisation; confirm none require historical development rows.
- Confirm the new user owns catalog items 1–13, has expression 1 and background 13 equipped, does not own item 14, and can unlock the Gold Crown through the current authenticated self-unlock flow.
- Verify Feedback and account-security pages load with test email configuration and USDA search fails gracefully or returns live results without requiring pre-existing food rows.
- Delete the disposable database after the check; never point this procedure at the normal development or production path.

## Account

- Register a fresh account and confirm the starter Capy state is available.
- Follow the confirmation email and verify the account can then sign in.
- Verify an unconfirmed account receives the intended generic sign-in guidance.
- Sign in and sign out using the normal navigation.
- Request a password reset, follow the link, set a new password and sign in with it.

## Profile

- Complete first-time setup in Metric mode and verify estimates are shown.
- Switch to Imperial mode; save feet/inches, current weight and goal weight, then reload and confirm the displayed values round-trip sensibly.
- Exercise Maintain, Lose and Gain, including every supported weekly goal.
- Confirm Maintain clears weight-change inputs.
- Switch between calculated and custom calorie targets; verify a valid custom target works independently of the unused calculation.
- Submit missing, malformed, out-of-range and inconsistent values and confirm field-specific validation appears.

## Foods and portions

- Create, edit and soft-delete a custom food in every supported mass/volume display unit.
- Add, edit and soft-delete a portion; verify displayed values continue to use the food's preferred unit.
- Confirm mass-to-mass and volume-to-volume changes remain possible.
- Confirm mass/volume dimension changes are blocked after a portion or diary history exists.
- Favourite and unfavourite foods and verify recent foods update after diary use.

## USDA search

- Search, paginate, change page size and select a result without browser console errors.
- Try a blank query, a query with no results and an invalid external ID.
- Favourite and unfavourite a USDA result and confirm lists stay synchronized.
- Log a selected result and verify nutrition comes from the server-resolved food.
- If practical, disable the USDA credential/network temporarily and verify cached foods remain usable while uncached foods show a friendly failure.

## Diary

- Log exact quantities in `g`, `kg`, `oz`, `lb`, `ml`, `L` and `fl oz`; verify totals, especially `2 oz` of a food defined per `1 oz`.
- Log whole and decimal portion counts and verify nutrition totals.
- Edit exact to portion and portion to exact; confirm stale fields do not survive the transition.
- Edit date, meal and quantity without changing the historical food snapshot.
- Edit/delete a food and portion after logging, then verify the old diary entry keeps its original name, serving basis, portion label and nutrition.
- Delete a diary entry and confirm the correct date is preserved on return.
- Navigate the minimum/maximum supported dates and submit malformed/out-of-range dates.
- Where practical, alter food, portion and diary IDs in requests and confirm another user's data is not exposed or changed.

## Theme

- Select Light, Dark and System while signed in; navigate and refresh to verify account persistence.
- Sign out and verify local browser preference is used without replacing the authenticated account preference.
- On a first anonymous visit, verify System follows the browser/OS preference.

## Comfy Capy

- Verify a fresh user receives the default expression/background and starter inventory, but not the non-starter crown.
- Equip and clear supported cosmetic slots; confirm the preview and navbar avatar update.
- Unlock the current MVP cosmetic and verify it remains unlocked after reload.
- Exercise category filtering and theme selection with keyboard and pointer input.
- If practical, open a legacy account missing starter state and verify provisioning repairs it.
- With two accounts, confirm inventory, unlocks and equipped appearance remain isolated.

## Feedback

- While signed out, open Feedback from the footer and submit a short message using development/test email configuration.
- Repeat while signed in and confirm the form does not request or reveal account information.
- Submit empty, whitespace-only and more-than-4,000-character feedback and confirm accessible validation.
- Simulate missing configuration/provider failure and confirm only the generic failure message appears.
- Make six rapid submissions in a test environment and confirm the sixth receives the rate-limit response.
- Check footer alignment on desktop and natural wrapping without horizontal overflow on a narrow viewport.
- Complete the form using only a keyboard and verify focus, validation and success/error announcements.

## General and isolation

- Repeat key Diary, Foods, Profile and Capy mutations with two accounts and manipulated IDs.
- Navigate all major pages using keyboard controls and perform a basic screen-reader sanity pass.
- Check important forms and tables at 200% and 400% zoom and on representative mobile widths.
- Verify primary navigation, empty/loading/error states and destructive confirmations.
- Check the browser console and server log for unexpected errors during the walkthrough.
