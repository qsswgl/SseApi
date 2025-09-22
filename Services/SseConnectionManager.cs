using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace SseApi.Services;

public class SseConnectionManager
{
    private readonly ILogger<SseConnectionManager> _logger;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Stream>> _userStreams = new();

    public SseConnectionManager(ILogger<SseConnectionManager> logger) => _logger = logger;

    public Task BroadcastAsync(string message)
    {
        _logger.LogInformation("SSE Broadcast: {Message}", message);
        return Task.CompletedTask;
    }

    // 兼容 title + object 的调用
    public Task BroadcastAsync(string title, object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        _logger.LogInformation("SSE Broadcast: {Title} - {Payload}", title, json);
        return Task.CompletedTask;
    }

    // 可选：兼容 message + CancellationToken
    public Task BroadcastAsync(string message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SSE Broadcast: {Message}", message);
        return Task.CompletedTask;
    }

    // Register a new SSE stream for a specific user
    public Guid Register(string userId, Stream responseStream)
    {
        var id = Guid.NewGuid();
        var bucket = _userStreams.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Stream>());
        bucket[id] = responseStream;
        _logger.LogInformation("SSE Register: user={UserId}, connection={ConnectionId}, total={Count}", userId, id, bucket.Count);
        return id;
    }

    // Unregister on disconnect
    public void Unregister(string userId, Guid connectionId)
    {
        if (_userStreams.TryGetValue(userId, out var bucket))
        {
            if (bucket.TryRemove(connectionId, out _))
            {
                _logger.LogInformation("SSE Unregister: user={UserId}, connection={ConnectionId}, remaining={Count}", userId, connectionId, bucket.Count);
            }

            if (bucket.IsEmpty)
            {
                _userStreams.TryRemove(userId, out _);
            }
        }
    }

    // Send message to a specific user
    public async Task<int> SendToUserAsync(string userId, string eventType, string message, CancellationToken ct = default)
    {
        if (!_userStreams.TryGetValue(userId, out var bucket) || bucket.IsEmpty)
        {
            _logger.LogInformation("SSE SendToUser: user={UserId} has no listeners", userId);
            return 0;
        }

        var payload = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(eventType))
        {
            payload.Append("event: ").Append(eventType).Append('\n');
        }
        payload.Append("data: ").Append(message ?? string.Empty).Append("\n\n");
        var buffer = Encoding.UTF8.GetBytes(payload.ToString());

        var sent = 0;
        foreach (var kvp in bucket)
        {
            var stream = kvp.Value;
            try
            {
                await stream.WriteAsync(buffer, 0, buffer.Length, ct);
                await stream.FlushAsync(ct);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SSE SendToUser failed: user={UserId}, connection={ConnectionId}", userId, kvp.Key);
            }
        }

        _logger.LogInformation("SSE SendToUser: user={UserId}, event={EventType}, delivered={Count}", userId, eventType, sent);
        return sent;
    }
}