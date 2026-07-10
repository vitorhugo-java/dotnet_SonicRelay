using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SonicRelay.Api.Services;
using SonicRelay.Domain.Users;

namespace SonicRelay.Api.Endpoints;

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/account")
            .RequireAuthorization()
            .WithTags("Account");

        // Self-service account deletion. Consumed by the Flutter and Windows apps.
        group.MapDelete("/", DeleteOwnAccountAsync).WithName("DeleteOwnAccount");
        return app;
    }

    private static async Task<IResult> DeleteOwnAccountAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> userManager,
        AccountDeletionService deletionService,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null) return Results.Unauthorized();

        var origin = httpContext.Connection.RemoteIpAddress?.ToString();
        var outcome = await deletionService.DeleteAsync(
            user.Id, user.Id, AccountDeletionReason.SelfService, origin, ct);

        // Self-service deletion of your own (existing) account cannot report NotFound,
        // and SelfDeletionForbidden only applies to the admin path.
        return outcome == AccountDeletionOutcome.Deleted
            ? Results.NoContent()
            : Results.StatusCode(StatusCodes.Status500InternalServerError);
    }
}
