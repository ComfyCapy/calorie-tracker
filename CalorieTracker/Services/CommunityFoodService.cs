using System.Security.Claims;
using CalorieTracker.Data;
using CalorieTracker.Models;
using CalorieTracker.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Services;

public sealed class CommunityFoodService(ApplicationDbContext db, IAuthorizationService authorization, TimeProvider clock)
{
    public static string? UserId(ClaimsPrincipal actor) => actor.Identity?.IsAuthenticated == true
        ? actor.FindFirstValue(ClaimTypes.NameIdentifier) : null;

    public async Task<CommunityFood?> SubmitAsync(ClaimsPrincipal actor, int foodId, CancellationToken ct = default)
    {
        var userId = UserId(actor);
        if (userId == null) return null;
        var food = await db.Foods.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == foodId && x.UserId == userId && x.Source == null && !x.IsDeleted, ct);
        if (food == null) return null;
        var existing = await db.CommunityFoods.SingleOrDefaultAsync(x => x.SourceFoodId == foodId, ct);
        if (existing != null) return existing;
        var validation = new ModelStateDictionary();
        if (!ValidationRules.ValidateFood(food, validation, "Food", out _) ||
            food.Name.Length > 200 || food.Name.Any(char.IsControl))
            throw new ArgumentException("Check the food's name, nutrition and serving details before submitting. Names must be 200 characters or fewer.");
        var snapshot = new CommunityFood
        {
            SourceFoodId = food.Id, SubmitterId = userId, SubmittedUtc = clock.GetUtcNow().UtcDateTime,
            Name = food.Name, Calories = food.Calories, Protein = food.Protein, Carbohydrates = food.Carbohydrates,
            Fat = food.Fat, ServingSize = food.ServingSize, CanonicalServingSize = food.CanonicalServingSize,
            ServingUnit = food.ServingUnit, ServingBasis = food.ServingBasis, PortionLabel = food.PortionLabel
        };
        db.CommunityFoods.Add(snapshot);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            db.Entry(snapshot).State = EntityState.Detached;
            return await db.CommunityFoods.SingleAsync(x => x.SourceFoodId == foodId, ct);
        }
        return snapshot;
    }

    public async Task<bool> ReviewAsync(ClaimsPrincipal actor, int id, bool approve, string? note, CancellationToken ct = default)
    {
        if (!(await authorization.AuthorizeAsync(actor, AccessRoles.Admin)).Succeeded)
            throw new UnauthorizedAccessException();
        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note?.Length > 500) throw new ArgumentException("Notes must be 500 characters or fewer.");
        // A conditional update makes the first review final, including concurrent reviews.
        return await db.CommunityFoods.Where(x => x.Id == id && x.Status == CommunityFoodStatus.Pending)
            .ExecuteUpdateAsync(update => update
                .SetProperty(x => x.Status, approve ? CommunityFoodStatus.Approved : CommunityFoodStatus.Rejected)
                .SetProperty(x => x.ReviewerId, UserId(actor))
                .SetProperty(x => x.ReviewedUtc, clock.GetUtcNow().UtcDateTime)
            .SetProperty(x => x.ModeratorNote, note), ct) == 1;
    }

    public async Task<bool> RenameAsync(ClaimsPrincipal actor, int id, string? name, CancellationToken ct = default)
    {
        if (!(await authorization.AuthorizeAsync(actor, AccessRoles.Admin)).Succeeded)
            throw new UnauthorizedAccessException();
        name = name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(char.IsControl))
            throw new ArgumentException("Names must be between 1 and 200 characters and cannot contain line breaks.");
        return await db.CommunityFoods.Where(x => x.Id == id)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.Name, name), ct) == 1;
    }

    public IQueryable<CommunityFoodSearchResult> Search(string? term, ClaimsPrincipal? actor = null)
    {
        var userId = actor == null ? null : UserId(actor);
        var pattern = "%" + (term ?? "").Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        return db.CommunityFoods.AsNoTracking().Where(x => x.Status == CommunityFoodStatus.Approved &&
            EF.Functions.Like(x.Name, pattern, "\\"))
            .Select(food => new
            {
                Food = food,
                Score = food.Votes.Sum(vote => (int?)vote.Value) ?? 0,
                CurrentUserVote = userId == null ? 0 : food.Votes
                    .Where(vote => vote.UserId == userId).Select(vote => vote.Value).FirstOrDefault()
            })
            .OrderByDescending(x => x.Score).ThenBy(x => x.Food.Name).ThenBy(x => x.Food.Id)
            .Select(x => new CommunityFoodSearchResult(x.Food, x.Score, x.CurrentUserVote));
    }

    public async Task<int?> VoteAsync(ClaimsPrincipal actor, int id, int value, CancellationToken ct = default)
    {
        if (value is not (-1 or 1)) throw new ArgumentException("Vote must be an upvote or downvote.");
        var userId = UserId(actor);
        if (userId == null) return null;
        if (!await db.CommunityFoods.AsNoTracking().AnyAsync(
                x => x.Id == id && x.Status == CommunityFoodStatus.Approved, ct))
            return null;

        var vote = await db.CommunityFoodVotes.SingleOrDefaultAsync(
            x => x.CommunityFoodId == id && x.UserId == userId, ct);
        if (vote == null)
        {
            vote = new CommunityFoodVote { CommunityFoodId = id, UserId = userId, Value = value };
            db.CommunityFoodVotes.Add(vote);
        }
        else if (vote.Value == value)
        {
            db.CommunityFoodVotes.Remove(vote);
        }
        else
        {
            vote.Value = value;
        }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 })
        {
            // A simultaneous first vote won the composite-key race. Re-read and
            // apply this request's normal toggle/switch semantics to that row.
            db.Entry(vote).State = EntityState.Detached;
            var winner = await db.CommunityFoodVotes.SingleAsync(
                x => x.CommunityFoodId == id && x.UserId == userId, ct);
            if (winner.Value == value) db.CommunityFoodVotes.Remove(winner);
            else winner.Value = value;
            await db.SaveChangesAsync(ct);
        }
        return await db.CommunityFoodVotes.Where(x => x.CommunityFoodId == id).SumAsync(x => (int?)x.Value, ct) ?? 0;
    }

    public async Task<Food?> SelectAsync(ClaimsPrincipal actor, int id, CancellationToken ct = default)
    {
        var userId = UserId(actor);
        if (userId == null) return null;
        var snapshot = await db.CommunityFoods.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == id && x.Status == CommunityFoodStatus.Approved, ct);
        if (snapshot == null) return null;
        var externalId = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var food = await db.Foods.SingleOrDefaultAsync(
            x => x.UserId == userId && x.Source == "Community" && x.ExternalId == externalId, ct);
        if (food != null)
        {
            food.IsDeleted = false;
            await db.SaveChangesAsync(ct);
            return food;
        }
        food = snapshot.ToPrivateFood(userId);
        db.Foods.Add(food);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: 2067 })
        {
            db.Entry(food).State = EntityState.Detached;
            return await db.Foods.SingleAsync(x => x.UserId == userId && x.Source == "Community" && x.ExternalId == externalId, ct);
        }
        return food;
    }
}
