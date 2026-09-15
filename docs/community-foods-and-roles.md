# Community Foods and access roles (V1)

Identity roles are Standard, Beta, Admin and Owner. Every registration receives Standard;
the original access-role migration seeds Standard/Beta/Admin and backfills Standard for
existing accounts. The additive Owner migration seeds Owner without assigning it.
Standard is the baseline. Normal authenticated features still work for older sessions or
accounts without a role claim. XP never grants roles. OwnerAccess, AdminAccess and
BetaAccess query current normalized Identity membership: Owner satisfies all three,
Admin satisfies AdminAccess and BetaAccess, and Beta satisfies only BetaAccess.

## Operations

Back up the production database and apply the reviewed EF migration using the normal
release procedure. Do not use EnsureCreated against an existing database. No migrations
run automatically at web startup. Role insertion is idempotent by normalized role name,
so an existing operator-created Standard/Beta/Admin/Owner role is retained. Existing
accounts keep their current memberships.

To assign the first Admin, from the deployed application directory with its normal
secure production environment/configuration, run:

    dotnet CalorieTracker.dll --grant-admin EXISTING_CONFIRMED_USER_ID

To assign Owner, use the corresponding operational-only command:

    dotnet CalorieTracker.dll --grant-owner EXISTING_CONFIRMED_USER_ID

Use the exact Identity user ID of an existing, verified account; confirm the account
identity first. This command runs locally under operator access, is idempotent, and exits
without starting a web server. It does not create accounts or accept passwords, emails,
or an arbitrary role. It refuses missing and unconfirmed users. Never expose it as an HTTP
endpoint or place it in normal service startup arguments. No production changes were
performed during implementation.

Owner grant/removal is never available in the UI. Before any manual operational removal,
ensure another confirmed elevated operator can access /Admin. Owner and Admin self-service
account deletion is blocked until the corresponding operational membership is removed.
Owners may grant/remove Beta and Admin in /Admin/Users. Admins may grant/remove Beta but
cannot manage Admin or modify an Owner. Posted role names are ignored.

## Data and behavior

CommunityFoods is one durable submission/catalogue table. A custom-food owner explicitly
submits a snapshot, then an Admin or Owner approves or rejects it. Admins and Owners may correct the catalogue
name in any moderation state; nutrition and serving data remain immutable.
Each source food may be submitted once (a unique index); retries return the existing
submission. A first-review-wins conditional update prevents competing approvals/rejections.
Rejected entries remain history; correction/resubmission of the same food is deferred.

Snapshots copy the existing serving basis, amount, unit/portion label and nutrition.
There is no brand field in the private Food model. Additional private FoodPortions are
not published. Source changes and soft/hard deletion do not change snapshots. EF rejects
tracked changes to snapshot values after insert. Public queries filter Approved only.
Reviewer/submitter identities and moderation notes never appear in public results.

CommunityFoodVotes stores one active +1/-1 vote per user and approved Community Food,
enforced by a composite primary key and value check constraint. Pressing the same vote
again removes it; pressing the opposite vote switches it. Scores are derived with SUM,
not cached, and matching results order by score descending, then name and stable ID.
Pending/rejected records reject voting. User and Community Food deletion cascade votes.

Selecting an approved item creates/reuses a user-owned Food with Source=Community and
ExternalId equal to the catalogue ID. Existing Diary validation and nutrition snapshot
capture remain authoritative. Public records are never referenced directly by Diary.
No XP or contributor achievements are awarded. Failure to submit cannot undo private
food creation because submission is a separate deliberate action.

SourceFoodId, SubmitterId and ReviewerId are nullable foreign keys with ON DELETE SET NULL.
Account deletion removes its normal private graph and anonymizes these catalogue links;
pending, approved and rejected snapshots remain as an anonymized moderation record. The
deleting user's votes cascade away. Other users' imported foods/Diary history survive.
Users are warned not to include personal information in public food names.

## Acceptance

- Register an account: Standard only, no role selector; create a private food normally.
- Use its action menu to submit it; confirm the privacy notice, snapshot and Pending status.
- Edit/delete the source and confirm the submitted values do not change.
- As Standard and Beta, directly request /Admin, /Admin/Users and /Admin/CommunityFoods:
  access must be denied, including POSTs and forged role/UserId/moderation fields.
- As Admin and Owner, see Admin navigation, counts and queue; approve/reject, rename, then repeat a review.
- Search Community from Foods: only approved items, one query/source selector, ranked bounded pages.
- Upvote, remove, downvote and switch; confirm active state, score, keyboard use and mobile wrapping.
- Add an approved item to Diary; check measured and direct-portion servings and history.
- Switch Database / Community / My Foods while keeping the query and Diary context.
- Confirm Admin can grant/remove Beta but cannot change Admin or any Owner account.
- Confirm Owner can grant/remove Beta and Admin, while Owner membership has no HTTP control.
- Test light/dark, 320px layout, keyboard focus, validation, and success/submitting states.
- Test account deletion with submitted and imported foods in a disposable database.

V1 has no nutrition correction/removal UI, bulk moderation, contributor reputation or resubmission.
Search uses SQLite LIKE (ASCII case-insensitive, literal percent/underscore), 20 items per page.
Admin user search returns at most 30 matches and asks for a more specific query.
