using System.Collections.Concurrent;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using SseApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 添加SSL证书相关服务
builder.Services.AddHttpClient<DnsPodApiClient>();
builder.Services.AddSingleton<DnsPodApiClient>();
builder.Services.AddSingleton<AcmeService>();
builder.Services.AddSingleton<CertificateRenewalService>();
builder.Services.AddHostedService<CertificateRenewalService>();

// 添加SSE连接管理服务
builder.Services.AddSingleton<SseConnectionManager>();
builder.Services.AddHostedService<HeartbeatService>();

var app = builder.Build();

// 配置 Kestrel 以支持 HTTPS
var certificateService = app.Services.GetService<CertificateRenewalService>();
if (certificateService != null)
{
    // 等待证书服务初始化
// 简化启动，避免复杂的证书检查逻辑
/*
    _ = Task.Run(async () =>
    {
        await Task.Delay(5000); // 给服务一些时间来加载证书
        var certificate = certificateService.CurrentCertificate;
        if (certificate != null)
        {
            app.Logger.LogInformation("SSL certificate loaded successfully. Valid until: {ExpiryDate}", certificate.NotAfter);
        }
        else
        {
            app.Logger.LogWarning("No SSL certificate available. HTTPS may not work properly.");
        }
    });
*/
app.Logger.LogInformation("应用程序启动，跳过证书检查");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
// 暂时禁用 HTTPS 重定向，因为我们只使用 HTTP 进行测试
// app.UseHttpsRedirection();

// 静态文件服务，用于提供测试页面
app.UseStaticFiles();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

// 原有的天气预报API
app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

// SSE连接端点
app.MapGet("/sse", async (HttpContext context, SseConnectionManager connectionManager) =>
{
    var response = context.Response;
    
    // 设置SSE响应头
    response.Headers["Content-Type"] = "text/event-stream";
    response.Headers["Cache-Control"] = "no-cache";
    response.Headers["Connection"] = "keep-alive";
    response.Headers["Access-Control-Allow-Origin"] = "*";
    response.Headers["Access-Control-Allow-Headers"] = "Cache-Control";

    var clientId = Guid.NewGuid().ToString();
    var connection = new SseConnection(clientId, response, context.RequestAborted);
    
    // 添加连接到管理器
    connectionManager.AddConnection(connection);
    
    try
    {
        // 发送连接成功消息
        await connection.SendEventAsync("connected", new { clientId, timestamp = DateTime.UtcNow });
        
        // 保持连接直到取消
        await Task.Delay(Timeout.Infinite, context.RequestAborted);
    }
    catch (OperationCanceledException)
    {
        // 连接被取消，正常断开
    }
    finally
    {
        // 移除连接
        connectionManager.RemoveConnection(clientId);
    }
});

// 获取当前连接状态
app.MapGet("/sse/status", (SseConnectionManager connectionManager) =>
{
    return new
    {
        activeConnections = connectionManager.GetConnectionCount(),
        connections = connectionManager.GetConnectionIds()
    };
});

// 广播消息到所有连接
app.MapPost("/sse/broadcast", async (BroadcastMessage message, SseConnectionManager connectionManager) =>
{
    await connectionManager.BroadcastAsync(message.EventType ?? "message", message.Data);
    return Results.Ok(new { sent = connectionManager.GetConnectionCount() });
});

// 发送消息到特定客户端
app.MapPost("/sse/send/{clientId}", async (string clientId, BroadcastMessage message, SseConnectionManager connectionManager) =>
{
    var success = await connectionManager.SendToClientAsync(clientId, message.EventType ?? "message", message.Data);
    return success ? Results.Ok() : Results.NotFound();
});

// SSL证书管理API
app.MapGet("/ssl/status", (CertificateRenewalService certificateService) =>
{
    var certificate = certificateService.CurrentCertificate;
    if (certificate == null)
    {
        return Results.Ok(new { hasCertificate = false, message = "No certificate loaded" });
    }

    return Results.Ok(new
    {
        hasCertificate = true,
        subject = certificate.Subject,
        issuer = certificate.Issuer,
        notBefore = certificate.NotBefore,
        notAfter = certificate.NotAfter,
        daysUntilExpiry = (certificate.NotAfter - DateTime.UtcNow).Days,
        isValid = certificate.NotAfter > DateTime.UtcNow
    });
});

app.MapPost("/ssl/renew", async (CertificateRenewalService certificateService) =>
{
    var success = await certificateService.ForceCertificateRenewalAsync();
    return success ? Results.Ok(new { message = "Certificate renewal initiated successfully" }) 
                  : Results.Problem("Failed to renew certificate");
});

// 测试 DNSPOD API 连接 - 简化版本，不依赖服务注入
app.MapGet("/dnspod/test", async () =>
{
    try
    {
        using var httpClient = new HttpClient();
        var body = "login_token=594534,a30b94f683079f0e36131c2653c77160&format=json";
        var content = new StringContent(body, System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
        
        var response = await httpClient.PostAsync("https://dnsapi.cn/Domain.List", content);
        var responseText = await response.Content.ReadAsStringAsync();
        
        return Results.Ok(new 
        { 
            message = "配置文件 API 凭据测试成功",
            statusCode = (int)response.StatusCode,
            success = response.IsSuccessStatusCode,
            timestamp = DateTime.Now,
            response = responseText.Length > 500 ? responseText.Substring(0, 500) + "..." : responseText
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"测试失败: {ex.Message}");
    }
});

// 提供测试页面
app.MapGet("/", () => Results.Redirect("/sse-test-page.html"));

app.Run();

// 数据模型
record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record BroadcastMessage(string? EventType, object Data);

// SSE连接类
public class SseConnection
{
    public string ClientId { get; }
    public HttpResponse Response { get; }
    public CancellationToken CancellationToken { get; }
    public DateTime ConnectedAt { get; }
    public DateTime LastHeartbeat { get; set; }

    public SseConnection(string clientId, HttpResponse response, CancellationToken cancellationToken)
    {
        ClientId = clientId;
        Response = response;
        CancellationToken = cancellationToken;
        ConnectedAt = DateTime.UtcNow;
        LastHeartbeat = DateTime.UtcNow;
    }

    public async Task SendEventAsync(string eventType, object data)
    {
        if (CancellationToken.IsCancellationRequested)
            return;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var message = $"event: {eventType}\ndata: {json}\n\n";
            
            await Response.WriteAsync(message, CancellationToken);
            await Response.Body.FlushAsync(CancellationToken);
            
            LastHeartbeat = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending SSE event to {ClientId}: {ex.Message}");
        }
    }

    public async Task SendHeartbeatAsync()
    {
        await SendEventAsync("heartbeat", new { timestamp = DateTime.UtcNow, clientId = ClientId });
    }
}

// SSE连接管理器
public class SseConnectionManager
{
    private readonly ConcurrentDictionary<string, SseConnection> _connections = new();

    public void AddConnection(SseConnection connection)
    {
        _connections.TryAdd(connection.ClientId, connection);
        Console.WriteLine($"SSE connection added: {connection.ClientId}. Total connections: {_connections.Count}");
    }

    public void RemoveConnection(string clientId)
    {
        if (_connections.TryRemove(clientId, out var connection))
        {
            Console.WriteLine($"SSE connection removed: {clientId}. Total connections: {_connections.Count}");
        }
    }

    public async Task BroadcastAsync(string eventType, object data)
    {
        var deadConnections = new List<string>();
        
        foreach (var kvp in _connections)
        {
            try
            {
                await kvp.Value.SendEventAsync(eventType, data);
            }
            catch
            {
                deadConnections.Add(kvp.Key);
            }
        }

        // 清理死连接
        foreach (var clientId in deadConnections)
        {
            RemoveConnection(clientId);
        }
    }

    public async Task<bool> SendToClientAsync(string clientId, string eventType, object data)
    {
        if (_connections.TryGetValue(clientId, out var connection))
        {
            try
            {
                await connection.SendEventAsync(eventType, data);
                return true;
            }
            catch
            {
                RemoveConnection(clientId);
                return false;
            }
        }
        return false;
    }

    public async Task SendHeartbeatsAsync()
    {
        var deadConnections = new List<string>();
        
        foreach (var kvp in _connections)
        {
            try
            {
                await kvp.Value.SendHeartbeatAsync();
            }
            catch
            {
                deadConnections.Add(kvp.Key);
            }
        }

        // 清理死连接
        foreach (var clientId in deadConnections)
        {
            RemoveConnection(clientId);
        }
    }

    public int GetConnectionCount() => _connections.Count;
    
    public string[] GetConnectionIds() => _connections.Keys.ToArray();

    public SseConnection[] GetConnections() => _connections.Values.ToArray();
}

// 心跳包后台服务
public class HeartbeatService : BackgroundService
{
    private readonly SseConnectionManager _connectionManager;
    private readonly ILogger<HeartbeatService> _logger;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30); // 30秒心跳间隔

    public HeartbeatService(SseConnectionManager connectionManager, ILogger<HeartbeatService> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("心跳包服务已启动，间隔: {Interval}", _heartbeatInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _connectionManager.SendHeartbeatsAsync();
                
                var connectionCount = _connectionManager.GetConnectionCount();
                if (connectionCount > 0)
                {
                    _logger.LogDebug("心跳包已发送到 {Count} 个连接", connectionCount);
                }

                await Task.Delay(_heartbeatInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 服务停止，正常退出
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "心跳包服务发生错误");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); // 错误后等待5秒再重试
            }
        }

        _logger.LogInformation("心跳包服务已停止");
    }
}
