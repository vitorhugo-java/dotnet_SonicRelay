using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SonicRelay.Api.IntegrationTests;

public sealed class AuthEndpointsTests : IClassFixture<SonicRelayApiFactory>
{
    private const string Email = "listener@example.com";
    private const string Password = "Valid1!Password";
    private readonly HttpClient _client;

    public AuthEndpointsTests(SonicRelayApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task Register_creates_an_identity_user()
    {
        var response = await RegisterAsync($"register-{Guid.NewGuid():N}@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_returns_access_and_refresh_tokens()
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await RegisterSuccessfullyAsync(email);

        var tokens = await LoginAsync(email);

        Assert.Equal("Bearer", tokens.GetProperty("tokenType").GetString());
        Assert.False(string.IsNullOrWhiteSpace(tokens.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(tokens.GetProperty("refreshToken").GetString()));
        Assert.True(tokens.GetProperty("expiresIn").GetInt32() > 0);
    }

    [Fact]
    public async Task Refresh_returns_a_new_usable_token_pair()
    {
        var email = $"refresh-{Guid.NewGuid():N}@example.com";
        await RegisterSuccessfullyAsync(email);
        var original = await LoginAsync(email);

        var response = await _client.PostAsJsonAsync("/auth/refresh", new
        {
            refreshToken = original.GetProperty("refreshToken").GetString()
        });
        var refreshed = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(refreshed.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(refreshed.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Me_returns_the_authenticated_user()
    {
        var email = $"me-{Guid.NewGuid():N}@example.com";
        await RegisterSuccessfullyAsync(email);
        var tokens = await LoginAsync(email);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());

        var response = await _client.GetAsync("/auth/me");
        var profile = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, profile.GetProperty("email").GetString());
        Assert.True(Guid.TryParse(profile.GetProperty("id").GetString(), out _));
    }

    [Theory]
    [InlineData("/auth/me", "GET")]
    [InlineData("/auth/logout", "POST")]
    [InlineData("/api/devices", "GET")]
    public async Task Protected_endpoints_reject_anonymous_requests(string uri, string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), uri);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_accepts_an_authenticated_bearer_request()
    {
        var email = $"logout-{Guid.NewGuid():N}@example.com";
        await RegisterSuccessfullyAsync(email);
        var tokens = await LoginAsync(email);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());

        var response = await _client.PostAsync("/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private Task<HttpResponseMessage> RegisterAsync(string email) =>
        _client.PostAsJsonAsync("/auth/register", new { email, password = Password });

    private async Task RegisterSuccessfullyAsync(string email)
    {
        var response = await RegisterAsync(email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<JsonElement> LoginAsync(string email)
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }
}
