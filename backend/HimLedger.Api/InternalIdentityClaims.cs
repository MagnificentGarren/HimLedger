using System.Security.Claims;
using HimLedger.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;

namespace HimLedger.Api;

internal static class InternalIdentityClaims
{
    public static async Task ResolveAsync(TokenValidatedContext context, bool isEntraToken)
    {
        var principal = context.Principal;
        var identity = principal?.Identity as ClaimsIdentity;
        if (principal is null || identity is null)
        {
            context.Fail("The authenticated identity is missing.");
            return;
        }

        var database = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        HimLedger.Domain.Entities.User? user;
        if (isEntraToken)
        {
            var tenantValue = principal.FindFirst("tid")?.Value;
            var objectValue = principal.FindFirst("oid")?.Value;
            if (!Guid.TryParse(tenantValue, out var tenantId)
                || !Guid.TryParse(objectValue, out var objectId))
            {
                context.Fail("The Entra token does not contain valid tenant and object identifiers.");
                return;
            }

            var tenant = tenantId.ToString("D");
            var objectIdText = objectId.ToString("D");
            user = await database.Users
                .Include(item => item.Role)
                .SingleOrDefaultAsync(
                    item => item.EntraTenantId == tenant && item.EntraObjectId == objectIdText,
                    context.HttpContext.RequestAborted);
        }
        else
        {
            var identifier = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(identifier, out var userId))
            {
                context.Fail("The local token does not contain an internal user identifier.");
                return;
            }

            user = await database.Users
                .Include(item => item.Role)
                .SingleOrDefaultAsync(item => item.UserId == userId, context.HttpContext.RequestAborted);
        }

        if (user is null || !user.IsActive)
        {
            context.Fail("The identity is not assigned to an active internal user account.");
            return;
        }

        foreach (var claim in identity.Claims.Where(IsAuthorizationClaim).ToArray())
        {
            identity.RemoveClaim(claim);
        }
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email));
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.Name));
        if (user.DepartmentId is int departmentId)
        {
            identity.AddClaim(new Claim("DepartmentId", departmentId.ToString()));
        }
    }

    private static bool IsAuthorizationClaim(Claim claim) =>
        claim.Type is ClaimTypes.NameIdentifier
            or ClaimTypes.Name
            or ClaimTypes.Email
            or ClaimTypes.Role
            or "role"
            or "roles"
            or "DepartmentId"
            or "departmentId";
}
