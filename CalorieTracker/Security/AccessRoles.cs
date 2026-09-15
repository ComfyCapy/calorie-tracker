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
    public const string Owner = "Owner";
    public const string OwnerAccess = "OwnerAccess";
    public const string AdminAccess = "AdminAccess";
    public const string BetaAccess = "BetaAccess";
    public const string NormalizedStandard = "STANDARD";
    public const string NormalizedBeta = "BETA";
    public const string NormalizedAdmin = "ADMIN";
    public const string NormalizedOwner = "OWNER";

    public static IdentityRole[] SeedRoles() => [
        new(Standard) { Id = "role-standard", NormalizedName = NormalizedStandard, ConcurrencyStamp = "role-standard" },
        new(Beta) { Id = "role-beta", NormalizedName = NormalizedBeta, ConcurrencyStamp = "role-beta" },
        new(Admin) { Id = "role-admin", NormalizedName = NormalizedAdmin, ConcurrencyStamp = "role-admin" },
        new(Owner) { Id = "role-owner", NormalizedName = NormalizedOwner, ConcurrencyStamp = "role-owner" }
    ];

    public static async Task AssignStandardAsync(UserManager<ApplicationUser> users, ApplicationUser user)
    {
        var result = await users.AddToRoleAsync(user, Standard);
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not assign the default account role.");
    }

    public static async Task GrantAdminOperationallyAsync(
        UserManager<ApplicationUser> users,
        string userId) =>
        await GrantOperationallyAsync(users, userId, Admin);

    public static async Task GrantOwnerOperationallyAsync(
        UserManager<ApplicationUser> users,
        string userId) =>
        await GrantOperationallyAsync(users, userId, Owner);

    private static async Task GrantOperationallyAsync(
        UserManager<ApplicationUser> users,
        string userId,
        string role)
    {
        var user = await users.FindByIdAsync(userId);
        if (user == null || !user.EmailConfirmed)
            throw new InvalidOperationException($"{role} grants require an existing email-confirmed account.");
        if (await users.IsInRoleAsync(user, role)) return;
        var result = await users.AddToRoleAsync(user, role);
        if (!result.Succeeded)
            throw new InvalidOperationException($"{role} grant failed.");
    }
}

public enum AccessLevel
{
    Owner,
    Admin,
    Beta
}

public sealed record AccessRoleRequirement(AccessLevel MinimumLevel) : IAuthorizationRequirement;

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
                       (role.NormalizedName == AccessRoles.NormalizedOwner ||
                        (requirement.MinimumLevel != AccessLevel.Owner &&
                         role.NormalizedName == AccessRoles.NormalizedAdmin) ||
                        (requirement.MinimumLevel == AccessLevel.Beta &&
                         role.NormalizedName == AccessRoles.NormalizedBeta))
                   select membership).AnyAsync())
            context.Succeed(requirement);
    }
}
