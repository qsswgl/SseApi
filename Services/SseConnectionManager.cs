using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SseApi.Services;

public class SseConnectionManager
{
    private readonly ILogger<SseConnectionManager> _logger;
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
}