using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SonicRelay.Api.Services;
using SonicRelay.Domain.Users;

namespace SonicRelay.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin");

        group.MapDelete("/users/{userId:guid}", DeleteUserAsync).WithName("AdminDeleteUser");
        return app;
    }

    private static async Task<IResult> DeleteUserAsync(
        Guid userId,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AccountDeletionService deletionService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var admin = await userManager.GetUserAsync(principal);
        if (admin is null) return Results.Unauthorized();

        var origin = httpContext.Connection.RemoteIpAddress?.ToString();
        var outcome = await deletionService.DeleteAsync(
            userId, admin.Id, AccountDeletionReason.AdminAction, origin, ct);

        return outcome switch
        {
            AccountDeletionOutcome.Deleted => Results.NoContent(),
            AccountDeletionOutcome.NotFound => Results.NotFound(),
            AccountDeletionOutcome.SelfDeletionForbidden => Results.BadRequest(
                new { error = "An admin cannot delete their own account through the admin endpoint." }),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}
