using System.Net.Http.Json;

namespace SonicRelay.Api.Services;

/// <summary>
/// Payload delivered to the account-deletion webhook (consumed by n8n to email an operator).
/// </summary>
public sealed record AccountDeletionNotification(
    Guid UserId,
    string? Email,
    string Reason,
    string? RequestOrigin,
    DateTimeOffset RequestedAt);

/// <summary>
/// Notifies an external automation (n8n) when an account is deleted.
/// </summary>
public interface IAccountDeletionNotifier
{
    Task NotifyAsync(AccountDeletionNotification notification, CancellationToken ct);
}

/// <summary>
/// No-op notifier used when no webhook is configured (e.g. tests, local dev).
/// </summary>
public sealed class NullAccountDeletionNotifier : IAccountDeletionNotifier
{
    public Task NotifyAsync(AccountDeletionNotification notification, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// Posts the deletion notification to a configured webhook URL. Failures are logged
/// but never propagate: a flaky automation must not block account deletion.
/// </summary>
public sealed class WebhookAccountDeletionNotifier(
    HttpClient httpClient,
    string webhookUrl,
    ILogger<WebhookAccountDeletionNotifier> logger) : IAccountDeletionNotifier
{
    public async Task NotifyAsync(AccountDeletionNotification notification, CancellationToken ct)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync(webhookUrl, notification, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Account deletion webhook returned {StatusCode} for user {UserId}",
                    (int)response.StatusCode, notification.UserId);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Account deletion webhook failed for user {UserId}", notification.UserId);
        }
    }
}
