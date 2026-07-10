using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SonicRelay.Api.Services;
using SonicRelay.Domain.Users;
using Xunit;

namespace SonicRelay.Api.IntegrationTests;

public sealed class AccountDeletionTests : IClassFixture<SonicRelayApiFactory>
{
    private const string Password = "Valid1!Password";
    private readonly SonicRelayApiFactory _factory;

    public AccountDeletionTests(SonicRelayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Admin_delete_user_requires_authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync($"/api/admin/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_delete_user_forbidden_for_non_admin()
    {
        var client = _factory.CreateClient();
        var (_, token) = await CreateUserAsync(client);
        Authorize(client, token);

        var response = await client.DeleteAsync($"/api/admin/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_delete_returns_not_found_for_unknown_user()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAsync(client);
        Authorize(client, adminToken);

        var response = await client.DeleteAsync($"/api/admin/users/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Admin_delete_disables_user_and_blocks_login()
    {
        var client = _factory.CreateClient();
        var (email, _) = await CreateUserAsync(client);
        var targetId = await GetUserIdAsync(email);

        var adminClient = _factory.CreateClient();
        var adminToken = await CreateAdminAsync(adminClient);
        Authorize(adminClient, adminToken);

        var delete = await adminClient.DeleteAsync($"/api/admin/users/{targetId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Admin_cannot_delete_their_own_account()
    {
        var client = _factory.CreateClient();
        var adminToken = await CreateAdminAsync(client, out var adminEmail);
        Authorize(client, adminToken);
        var adminId = await GetUserIdAsync(adminEmail);

        var response = await client.DeleteAsync($"/api/admin/users/{adminId}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Self_delete_requires_authentication()
    {
        var client = _factory.CreateClient();

        var response = await client.DeleteAsync("/api/account");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Self_delete_disables_account_and_blocks_login()
    {
        var client = _factory.CreateClient();
        var (email, token) = await CreateUserAsync(client);
        Authorize(client, token);

        var response = await client.DeleteAsync("/api/account");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var login = await _factory.CreateClient()
            .PostAsJsonAsync("/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    private static void Authorize(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<(string Email, string Token)> CreateUserAsync(HttpClient client)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var register = await client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await ReadJsonAsync(login);
        return (email, tokens.GetProperty("accessToken").GetString()!);
    }

    private Task<string> CreateAdminAsync(HttpClient client) => CreateAdminAsync(client, out _);

    private Task<string> CreateAdminAsync(HttpClient client, out string email)
    {
        email = $"admin-{Guid.NewGuid():N}@example.com";
        return CreateAdminInternalAsync(client, email);
    }

    private async Task<string> CreateAdminInternalAsync(HttpClient client, string email)
    {
        var register = await client.PostAsJsonAsync("/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        await PromoteToAdminAsync(email);
        var login = await client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await ReadJsonAsync(login);
        return tokens.GetProperty("accessToken").GetString()!;
    }

    private async Task PromoteToAdminAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (!await roleManager.RoleExistsAsync(IdentitySeeder.AdminRole))
        {
            await roleManager.CreateAsync(new IdentityRole<Guid>(IdentitySeeder.AdminRole));
        }
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        await userManager.AddToRoleAsync(user!, IdentitySeeder.AdminRole);
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        return user!.Id;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }
}
