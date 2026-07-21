using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SonicRelay.Api.Contracts;
using Xunit;

namespace SonicRelay.Api.IntegrationTests;

public sealed class PairingChallengeTests : IClassFixture<SonicRelayApiFactory>
{
    private readonly HttpClient _client;

    public PairingChallengeTests(SonicRelayApiFactory factory) => _client = factory.CreateClient();

    private async Task<string> BootstrapAndAuthenticateAsync(string type, string platform)
    {
        var bootstrap = await (await _client.PostAsJsonAsync("/api/devices/bootstrap",
            new BootstrapDeviceRequest("Device", type, platform)))
            .Content.ReadFromJsonAsync<BootstrapDeviceResponse>();
        var token = await (await _client.PostAsJsonAsync("/api/devices/token",
            new DeviceTokenRequest(bootstrap!.DeviceId, bootstrap.CredentialSecret)))
            .Content.ReadFromJsonAsync<DeviceTokenResponse>();
        return token!.AccessToken;
    }

    private HttpRequestMessage Authorized(HttpMethod method, string url, string accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    [Fact]
    public async Task Publisher_Creates_Challenge_And_Viewer_Completes_Pairing()
    {
        var publisherToken = await BootstrapAndAuthenticateAsync("windows_publisher", "windows");
        var viewerToken = await BootstrapAndAuthenticateAsync("flutter_viewer", "android");

        var challengeResponse = await _client.SendAsync(
            Authorized(HttpMethod.Post, "/api/pairings/challenges", publisherToken));
        Assert.Equal(HttpStatusCode.Created, challengeResponse.StatusCode);
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<CreateChallengeResponse>();
        Assert.NotNull(challenge);
        Assert.Contains(challenge!.ChallengeId.ToString(), challenge.QrPayload);

        var completeResponse = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/pairings/complete",
            viewerToken, new CompletePairingRequest(challenge.ChallengeId, challenge.Code)));

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var pairing = await completeResponse.Content.ReadFromJsonAsync<PairingResponse>();
        Assert.Equal("active", pairing!.Status);
    }

    [Fact]
    public async Task Complete_Rejects_WrongCode_Without_Revealing_Reason()
    {
        var publisherToken = await BootstrapAndAuthenticateAsync("windows_publisher", "windows");
        var viewerToken = await BootstrapAndAuthenticateAsync("flutter_viewer", "android");
        var challengeResponse = await _client.SendAsync(
            Authorized(HttpMethod.Post, "/api/pairings/challenges", publisherToken));
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<CreateChallengeResponse>();

        var wrongCode = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/pairings/complete",
            viewerToken, new CompletePairingRequest(challenge!.ChallengeId, "WRONGCODE")));
        var unknownChallenge = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/pairings/complete",
            viewerToken, new CompletePairingRequest(Guid.NewGuid(), challenge.Code)));

        Assert.Equal(HttpStatusCode.BadRequest, wrongCode.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknownChallenge.StatusCode);
    }

    [Fact]
    public async Task Complete_Rejects_Code_After_MaxAttempts()
    {
        var publisherToken = await BootstrapAndAuthenticateAsync("windows_publisher", "windows");
        var viewerToken = await BootstrapAndAuthenticateAsync("flutter_viewer", "android");
        var challengeResponse = await _client.SendAsync(
            Authorized(HttpMethod.Post, "/api/pairings/challenges", publisherToken));
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<CreateChallengeResponse>();

        for (var i = 0; i < 5; i++)
        {
            await _client.SendAsync(Authorized(HttpMethod.Post, "/api/pairings/complete",
                viewerToken, new CompletePairingRequest(challenge!.ChallengeId, "WRONGCODE")));
        }

        var finalAttempt = await _client.SendAsync(Authorized(HttpMethod.Post, "/api/pairings/complete",
            viewerToken, new CompletePairingRequest(challenge!.ChallengeId, challenge.Code)));

        Assert.Equal(HttpStatusCode.BadRequest, finalAttempt.StatusCode);
    }

    [Fact]
    public async Task Only_PublisherDevices_Can_Create_Challenges()
    {
        var viewerToken = await BootstrapAndAuthenticateAsync("flutter_viewer", "android");

        var response = await _client.SendAsync(
            Authorized(HttpMethod.Post, "/api/pairings/challenges", viewerToken));

        // A viewer's token never carries the "pairing:create" scope (see
        // DeviceCredentialService.ScopesFor in Task 2), so the "pairing:create"
        // policy itself rejects the request before the handler runs.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
