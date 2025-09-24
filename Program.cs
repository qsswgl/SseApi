using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using SseApi.Services; // 证书与SSE服务
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

// 在独立部署场景下，内容根目录已设置为可执行文件所在目录
// 相对路径例如 ./certificates 与 wwwroot 都能正确解析

// 如果在 Windows 上运行，启用 Windows 服务集成与事件日志
if (OperatingSystem.IsWindows())
{
    builder.Host.UseWindowsService();
    builder.Logging.AddEventLog();
}

// 基础服务
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// 证书与DNSPOD相关服务
builder.Services.AddSingleton<DnsPodApiClient>();
builder.Services.AddSingleton<AcmeService>();
builder.Services.AddHostedService<CertificateRenewalService>();

// 新增：注册 SseConnectionManager
builder.Services.AddSingleton<SseConnectionManager>();

// 配置 Kestrel：显式监听端口并在 HTTPS 上开启 HTTP/3（客户端与系统不支持时将自动回退到 H2/H1）
builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP: 开启 HTTP/1 与 HTTP/2（便于反代或明文调试）
    options.ListenAnyIP(5000, listen =>
    {
        listen.Protocols = HttpProtocols.Http1AndHttp2;
    });

    // HTTPS: 开启 HTTP/1 + HTTP/2 + HTTP/3，并保留证书选择逻辑
    options.ListenAnyIP(5001, listen =>
    {
        listen.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
        listen.UseHttps(httpsOptions =>
        {
            httpsOptions.ServerCertificateSelector = (connCtx, name) =>
            {
                try
                {
                    var config = builder.Configuration;
                    var domain = config["SslCertificate:Domain"] ?? "qsgl.net";
                    var storePath = config["SslCertificate:CertificateStorePath"] ?? "./certificates";
                    var pfxPath = System.IO.Path.Combine(storePath, $"{domain}.pfx");
                    var passwordPath = System.IO.Path.Combine(storePath, $"{domain}.password");

                    if (System.IO.File.Exists(pfxPath))
                    {
                        var pwd = System.IO.File.Exists(passwordPath) ? System.IO.File.ReadAllText(passwordPath) : string.Empty;
                        var certBytes = System.IO.File.ReadAllBytes(pfxPath);
                        return X509CertificateLoader.LoadPkcs12(certBytes, pwd);
                    }
                }
                catch
                {
                }
                return null;
            };
        });
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseDefaultFiles();
app.UseStaticFiles();

// SSE endpoint（使用 Append 避免重复键警告）
app.MapGet("/sse", async context =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");
    context.Response.Headers.Append("Access-Control-Allow-Origin", "*");

    try
    {
        while (!context.RequestAborted.IsCancellationRequested)
        {
            await context.Response.WriteAsync($"data: {{\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",\"message\":\"Server is running\"}}\n\n");
            await context.Response.Body.FlushAsync();
            await Task.Delay(5000, context.RequestAborted);
        }
    }
    catch { /* 连接中断忽略 */ }
});

// 兼容别名：/api/sse/stream（与 /sse 相同实现）
app.MapGet("/api/sse/stream", async context =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");
    context.Response.Headers.Append("Access-Control-Allow-Origin", "*");

    try
    {
        while (!context.RequestAborted.IsCancellationRequested)
        {
            await context.Response.WriteAsync($"data: {{\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",\"message\":\"Server is running\"}}\n\n");
            await context.Response.Body.FlushAsync();
            await Task.Delay(5000, context.RequestAborted);
        }
    }
    catch { }
});

// 按用户ID的 SSE 流（新）：/sse/UsersID/{UsersID}
app.MapGet("/sse/UsersID/{UsersID}", async (HttpContext context, string UsersID, SseConnectionManager sse) =>
{
    context.Response.Headers.Append("Content-Type", "text/event-stream");
    context.Response.Headers.Append("Cache-Control", "no-cache");
    context.Response.Headers.Append("Connection", "keep-alive");
    context.Response.Headers.Append("Access-Control-Allow-Origin", "*");

    var connectionId = sse.Register(UsersID, context.Response.Body);

    try
    {
        // 初始欢迎消息
        await context.Response.WriteAsync($"event: init\n");
        await context.Response.WriteAsync($"data: {{\"UsersID\":\"{UsersID}\",\"connectedAt\":\"{DateTime.UtcNow:o}\"}}\n\n");
        await context.Response.Body.FlushAsync();

        // 心跳
        while (!context.RequestAborted.IsCancellationRequested)
        {
            await context.Response.WriteAsync($"event: heartbeat\n");
            await context.Response.WriteAsync($"data: {{\"ts\":\"{DateTime.UtcNow:o}\"}}\n\n");
            await context.Response.Body.FlushAsync();
            await Task.Delay(15000, context.RequestAborted);
        }
    }
    catch { }
    finally
    {
        sse.Unregister(UsersID, connectionId);
    }
});

// 简单状态
app.MapGet("/sse/status", () => Results.Json(new { Status = "OK", Timestamp = DateTime.Now }));

// 发送消息到 SSE（当前实现仅记录日志并返回 success）
app.MapPost("/sse/send", async (HttpRequest request, ILoggerFactory loggerFactory) =>
{
    try
    {
        string body;
        using (var reader = new StreamReader(request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        string? message = null;
        string? eventType = "message";
        if (!string.IsNullOrWhiteSpace(body))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            eventType = root.TryGetProperty("eventType", out var e) ? e.GetString() : "message";
        }

        var logger = loggerFactory.CreateLogger("SseSend");
        logger.LogInformation("SSE Send: type={EventType}, message={Message}", eventType, message);
        return Results.Json(new { success = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 400);
    }
});

// 别名：/api/sse/send
app.MapPost("/api/sse/send", async (HttpRequest request, ILoggerFactory loggerFactory) =>
{
    try
    {
        string body;
        using (var reader = new StreamReader(request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        string? message = null;
        string? eventType = "message";
        if (!string.IsNullOrWhiteSpace(body))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            eventType = root.TryGetProperty("eventType", out var e) ? e.GetString() : "message";
        }

        var logger = loggerFactory.CreateLogger("SseSend");
        logger.LogInformation("SSE Send (api): type={EventType}, message={Message}", eventType, message);
        return Results.Json(new { success = true });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 400);
    }
});

// 给指定用户发送（新）：/sse/UsersID/{UsersID}/send
app.MapPost("/sse/UsersID/{UsersID}/send", async (HttpRequest request, string UsersID, SseConnectionManager sse) =>
{
    try
    {
        string body;
        using (var reader = new StreamReader(request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        string? message = null;
        string eventType = "message";
        if (!string.IsNullOrWhiteSpace(body))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(body);
            var root = doc.RootElement;
            message = root.TryGetProperty("message", out var m) ? m.GetString() : null;
            eventType = root.TryGetProperty("eventType", out var e) ? e.GetString() ?? "message" : "message";
        }

        var ids = UsersID
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToArray();

        var perUsers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var total = 0;
        foreach (var id in ids)
        {
            var count = await sse.SendToUserAsync(id, eventType, message ?? string.Empty, request.HttpContext.RequestAborted);
            perUsers[id] = count;
            total += count;
        }

        return Results.Json(new { success = true, targets = ids, delivered = total, perUsers });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, error = ex.Message }, statusCode: 400);
    }
});

// SSL 状态查看
app.MapGet("/ssl/status", async (AcmeService acme, IConfiguration config) =>
{
    var domain = config["SslCertificate:Domain"] ?? "qsgl.net";
    var storePath = config["SslCertificate:CertificateStorePath"] ?? "./certificates";
    var pfxPath = System.IO.Path.Combine(storePath, $"{domain}.pfx");
    if (!System.IO.File.Exists(pfxPath))
        return Results.Json(new { hasCertificate = false });

    try
    {
        var pfxBytes = await System.IO.File.ReadAllBytesAsync(pfxPath);
        var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, "");
        return Results.Json(new
        {
            hasCertificate = true,
            subject = cert.Subject,
            notBefore = cert.NotBefore,
            notAfter = cert.NotAfter
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new { hasCertificate = false, error = ex.Message });
    }
});

// 手动触发证书申请/续签
app.MapPost("/ssl/renew", async (AcmeService acme) =>
{
    var cert = await acme.RequestCertificateAsync();
    if (cert == null) return Results.BadRequest(new { message = "renew failed" });
    return Results.Ok(new { message = "renew ok", notAfter = cert.NotAfter });
});

// DnsPod API 测试
app.MapGet("/dnspod/test", async (DnsPodApiClient dns) =>
{
    var result = await dns.GetDomainsAsync();
    return Results.Json(result);
});

// 静态测试页
app.MapGet("/", () => Results.Redirect("/sse-test-page.html"));

// 友好路由：/UsersID/{UsersID} -> 加载接收测试页并由前端解析 UsersID
app.MapGet("/UsersID/{UsersID}", (string UsersID) => Results.Redirect($"/sse-recv.html?UsersID={Uri.EscapeDataString(UsersID)}"));

// 兼容旧路由别名（不建议继续使用）：/sse/users/{userId} 与 /sse/users/{userId}/send
app.MapGet("/sse/users/{userId}", (HttpContext ctx, string userId) => Results.Redirect($"/sse/UsersID/{Uri.EscapeDataString(userId)}"));
app.MapPost("/sse/users/{userId}/send", (HttpContext ctx, string userId) => Results.Redirect($"/sse/UsersID/{Uri.EscapeDataString(userId)}/send"));

app.Run();

// 注意：顶级语句必须位于类型声明之前，所以下面的类型声明放在最后，且其后不再有任何顶级语句
public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
