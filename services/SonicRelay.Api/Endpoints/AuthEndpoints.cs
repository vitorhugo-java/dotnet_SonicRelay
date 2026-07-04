using Microsoft.AspNetCore.Identity;
using SonicRelay.Domain.Users;

namespace SonicRelay.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");
        group.MapIdentityApi<ApplicationUser>();

        group.MapPost("/logout", () => TypedResults.NoContent())
            .RequireAuthorization()
            .WithName("Logout");

        group.MapGet("/me", async (System.Security.Claims.ClaimsPrincipal principal,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.GetUserAsync(principal);
            return user is null
                ? Results.Unauthorized()
                : Results.Ok(new
                {
                    user.Id,
                    user.Email,
                    user.DisplayName,
                    user.EmailConfirmed,
                    user.CreatedAt,
                    user.LastLoginAt
                });
        })
            .RequireAuthorization()
            .WithName("GetCurrentUser");
        return app;
    }
}
