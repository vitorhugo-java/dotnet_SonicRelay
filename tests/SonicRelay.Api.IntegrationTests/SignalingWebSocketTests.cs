using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SonicRelay.Domain.Devices;
using SonicRelay.Domain.Sessions;
using SonicRelay.Infrastructure.Persistence;
using Xunit;

namespace SonicRelay.Api.IntegrationTests;

public sealed class SignalingWebSocketTests : IClassFixture<SonicRelayApiFactory>
{
    private const string Password = "Valid1!Password";
    private readonly SonicRelayApiFactory _factory;

    public SignalingWebSocketTests(SonicRelayApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Signaling_rejects_an_unauthenticated_websocket_upgrade()
    {
        var client = _factory.Server.CreateWebSocketClient();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ConnectAsync(
            new Uri($"ws://localhost/ws/signaling?sessionId={Guid.NewGuid()}&deviceId={Guid.NewGuid()}"),
            CancellationToken.None));

        Assert.Contains("401", exception.Message);
    }

    [Fact]
    public async Task Signaling_does_not_route_to_a_participant_in_another_session()
    {
        var sender = await CreateParticipantAsync("sender");
        var receiver = await CreateParticipantAsync("receiver");
        using var senderSocket = await ConnectAsync(sender);
        using var receiverSocket = await ConnectAsync(receiver);
        await ReceiveAsync(senderSocket);
        await ReceiveAsync(receiverSocket);

        await SendAsync(senderSocket, new
        {
            type = "webrtc.offer",
            to = receiver.ParticipantId,
            payload = new { sdp = "sensitive-test-sdp" }
        });

        var error = await ReceiveAsync(senderSocket);
        Assert.Equal("error", error.GetProperty("type").GetString());
        Assert.Equal("participant_not_found", error.GetProperty("code").GetString());

        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ReceiveAsync(receiverSocket, timeout.Token));
    }

    private async Task<TestParticipant> CreateParticipantAsync(string prefix)
    {
        var http = _factory.CreateClient();
        var email = $"ws-{prefix}-{Guid.NewGuid():N}@example.com";
        var register = await http.PostAsJsonAsync("/auth/register", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var login = await http.PostAsJsonAsync("/auth/login", new { email, password = Password });
        var tokens = await ReadJsonAsync(login);
        var accessToken = tokens.GetProperty("accessToken").GetString()!;

        var userId = await GetUserIdAsync(email);
        var deviceId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var participantId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Devices.Add(new Device
        {
            Id = deviceId,
            OwnerUserId = userId,
            Name = $"{prefix} device",
            Type = DeviceTypes.WindowsPublisher,
            Platform = DevicePlatforms.Windows,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.StreamSessions.Add(new StreamSession
        {
            Id = sessionId,
            OwnerUserId = userId,
            SourceDeviceId = deviceId,
            Status = SessionStatuses.Active,
            MaxViewers = 1,
            CodeExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.SessionParticipants.Add(new SessionParticipant
        {
            Id = participantId,
            SessionId = sessionId,
            UserId = userId,
            DeviceId = deviceId,
            Role = ParticipantRoles.Publisher,
            Status = ParticipantStatuses.Connected,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return new TestParticipant(accessToken, sessionId, deviceId, participantId);
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users
            .Where(x => x.Email == email)
            .Select(x => x.Id)
            .SingleAsync();
    }

    private async Task<WebSocket> ConnectAsync(TestParticipant participant)
    {
        var client = _factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
            request.Headers.Authorization = $"Bearer {participant.AccessToken}";
        return await client.ConnectAsync(
            new Uri($"ws://localhost/ws/signaling?sessionId={participant.SessionId}&deviceId={participant.DeviceId}"),
            CancellationToken.None);
    }

    private static async Task SendAsync(WebSocket socket, object message)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message);
        await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private static async Task<JsonElement> ReceiveAsync(WebSocket socket, CancellationToken ct = default)
    {
        var buffer = new byte[4096];
        var result = await socket.ReceiveAsync(buffer, ct);
        Assert.Equal(WebSocketMessageType.Text, result.MessageType);
        using var document = JsonDocument.Parse(buffer.AsMemory(0, result.Count));
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.Clone();
    }

    private sealed record TestParticipant(string AccessToken, Guid SessionId, Guid DeviceId, Guid ParticipantId);
}
