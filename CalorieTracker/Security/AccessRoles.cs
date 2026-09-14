using System.Security.Claims;
using CalorieTracker.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CalorieTracker.Security;

public static class AccessRoles
{
    public const string Standard = "Standard";
    public const string Beta = "Beta";
    public const string Admin = "Admin";
    public const string BetaAccess = "BetaAccess";

    public static IdentityRole[] SeedRoles() => [
        new(Standard) { Id = "role-standard", NormalizedName = "STANDARD", ConcurrencyStamp = "role-standard" },
        new(Beta) { Id = "role-beta", NormalizedName = "BETA", ConcurrencyStamp = "role-beta" },
        new(Admin) { Id = "role-admin", NormalizedName = "ADMIN", ConcurrencyStamp = "role-admin" }
    ];

    public static async Task AssignStandardAsync(UserManager<ApplicationUser> users, ApplicationUser user)
    {
        var result = await users.AddToRoleAsync(user, Standard);
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not assign the default account role.");
    }

    public static async Task GrantAdminOperationallyAsync(
        UserManager<ApplicationUser> users,
        string userId)
    {
        var user = await users.FindByIdAsync(userId);
        if (user == null || !user.EmailConfirmed)
            throw new InvalidOperationException("Admin grants require an existing email-confirmed account.");
        if (await users.IsInRoleAsync(user, Admin)) return;
        var result = await users.AddToRoleAsync(user, Admin);
        if (!result.Succeeded)
            throw new InvalidOperationException("Admin grant failed.");
    }
}

public sealed record AccessRoleRequirement(bool AllowBeta = false) : IAuthorizationRequirement;

// Consult current Identity membership so revoked access never waits for cookie refresh.
public sealed class AccessRoleHandler(ApplicationDbContext db)
    : AuthorizationHandler<AccessRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, AccessRoleRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated != true || userId == null) return;
        if (await (from membership in db.UserRoles
                   join role in db.Roles on membership.RoleId equals role.Id
                   where membership.UserId == userId &&
                       (role.Name == AccessRoles.Admin || (requirement.AllowBeta && role.Name == AccessRoles.Beta))
                   select membership).AnyAsync())
            context.Succeed(requirement);
    }
}
