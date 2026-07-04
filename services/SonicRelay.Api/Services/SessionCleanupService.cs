using Microsoft.EntityFrameworkCore;
using SonicRelay.Application.Abstractions;
using SonicRelay.Domain.Sessions;
using SonicRelay.Infrastructure.Persistence;

namespace SonicRelay.Api.Services;

public sealed class SessionCleanupService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SessionCleanupService> logger) : BackgroundService
{
    public async Task CleanupOnceAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var codeStore = scope.ServiceProvider.GetRequiredService<ISessionCodeStore>();
        var now = DateTimeOffset.UtcNow;
        var retention = TimeSpan.FromHours(configuration.GetValue(
            "Sessions:DisconnectedParticipantRetentionHours", 24));
        var participantCutoff = now.Subtract(retention);

        var expiredSessions = await db.StreamSessions
            .Where(x => (x.Status == SessionStatuses.Waiting || x.Status == SessionStatuses.Active)
                && x.CodeExpiresAt <= now)
            .ToListAsync(ct);
        foreach (var session in expiredSessions)
        {
            session.Status = SessionStatuses.Expired;
        }

        var staleParticipants = await db.SessionParticipants
            .Where(x => x.Status == ParticipantStatuses.Disconnected
                && x.LeftAt != null && x.LeftAt <= participantCutoff)
            .ToListAsync(ct);
        db.SessionParticipants.RemoveRange(staleParticipants);
        await db.SaveChangesAsync(ct);

        foreach (var session in expiredSessions)
        {
            await codeStore.RemoveAsync(session.Id, ct);
        }

        if (expiredSessions.Count > 0 || staleParticipants.Count > 0)
        {
            logger.LogInformation(
                "Session cleanup expired {ExpiredSessionCount} sessions and removed {ParticipantCount} disconnected participants",
                expiredSessions.Count, staleParticipants.Count);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Sessions:CleanupEnabled", true)) return;

        var interval = TimeSpan.FromSeconds(configuration.GetValue("Sessions:CleanupIntervalSeconds", 60));
        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                await CleanupOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Session cleanup pass failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
