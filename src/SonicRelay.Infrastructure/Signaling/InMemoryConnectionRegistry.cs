using System.Collections.Concurrent;
using SonicRelay.Application.Abstractions;

namespace SonicRelay.Infrastructure.Signaling;

public sealed class InMemoryConnectionRegistry : IConnectionRegistry
{
    private readonly ConcurrentDictionary<string, RegisteredConnection> _connections = new();

    public Task RegisterAsync(ConnectionDescriptor connection, CancellationToken ct)
    {
        _connections[connection.ConnectionId] = new RegisteredConnection(connection);
        return Task.CompletedTask;
    }

    public Task UnregisterAsync(string connectionId, CancellationToken ct)
    {
        _connections.TryRemove(connectionId, out _);
        return Task.CompletedTask;
    }

    public Task<ConnectionDescriptor?> FindByParticipantAsync(Guid participantId, CancellationToken ct)
    {
        var connection = _connections.Values.FirstOrDefault(x => x.Descriptor.ParticipantId == participantId);
        return Task.FromResult(connection?.Descriptor);
    }

    public Task<IReadOnlyList<ConnectionDescriptor>> ListBySessionAsync(Guid sessionId, CancellationToken ct)
    {
        var connections = _connections.Values.Where(x => x.Descriptor.SessionId == sessionId)
            .Select(x => x.Descriptor).ToArray();
        return Task.FromResult<IReadOnlyList<ConnectionDescriptor>>(connections);
    }

    public async Task<bool> SendToParticipantAsync(Guid sessionId, Guid participantId,
        ReadOnlyMemory<byte> message, CancellationToken ct)
    {
        var connection = _connections.Values.FirstOrDefault(x =>
            x.Descriptor.SessionId == sessionId && x.Descriptor.ParticipantId == participantId);
        if (connection is null) return false;
        await connection.SendAsync(message, ct);
        return true;
    }

    private sealed class RegisteredConnection(ConnectionDescriptor descriptor)
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        public ConnectionDescriptor Descriptor { get; } = descriptor;

        public async Task SendAsync(ReadOnlyMemory<byte> message, CancellationToken ct)
        {
            await _sendLock.WaitAsync(ct);
            try
            {
                await Descriptor.SendAsync(message, ct);
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}
