using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using SseApi.Services; // 证书与SSE服务

var builder = WebApplication.CreateBuilder(args);

// 基础服务
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// 证书与DNSPOD相关服务
builder.Services.AddSingleton<DnsPodApiClient>();
builder.Services.AddSingleton<AcmeService>();
builder.Services.AddHostedService<CertificateRenewalService>();

// 新增：注册 SseConnectionManager
builder.Services.AddSingleton<SseConnectionManager>();

// 配置 Kestrel 使用已申请到的证书（若存在）
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
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
                    // 使用推荐的加载方式
                    return X509CertificateLoader.LoadPkcs12(certBytes, pwd);
                }
            }
            catch
            {
                // 忽略选择失败，使用开发证书
            }
            return null;
        };
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

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

// 简单状态
app.MapGet("/sse/status", () => Results.Json(new { Status = "OK", Timestamp = DateTime.Now }));

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

app.Run();

// 注意：顶级语句必须位于类型声明之前，所以下面的类型声明放在最后，且其后不再有任何顶级语句
public record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
