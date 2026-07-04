using Microsoft.Extensions.Caching.Distributed;
using SonicRelay.Application.Abstractions;

namespace SonicRelay.Infrastructure.Redis;

public sealed class RedisSessionCodeStore(IDistributedCache cache) : ISessionCodeStore
{
    public async Task StoreAsync(string codeHash, Guid sessionId, TimeSpan ttl, CancellationToken ct)
    {
        await RemoveAsync(sessionId, ct);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        };
        await cache.SetStringAsync(CodeKey(codeHash), sessionId.ToString(), options, ct);
        await cache.SetStringAsync(CurrentCodeKey(sessionId), codeHash, options, ct);
    }

    public async Task<Guid?> RedeemAsync(string codeHash, CancellationToken ct)
    {
        var value = await cache.GetStringAsync(CodeKey(codeHash), ct);
        return Guid.TryParse(value, out var sessionId) ? sessionId : null;
    }

    public async Task RemoveAsync(Guid sessionId, CancellationToken ct)
    {
        var currentKey = CurrentCodeKey(sessionId);
        var codeHash = await cache.GetStringAsync(currentKey, ct);
        if (codeHash is not null)
        {
            await cache.RemoveAsync(CodeKey(codeHash), ct);
        }

        await cache.RemoveAsync(currentKey, ct);
    }

    private static string CodeKey(string codeHash) => $"sr:session-code:{codeHash}";
    private static string CurrentCodeKey(Guid sessionId) => $"sr:session-code-current:{sessionId}";
}
