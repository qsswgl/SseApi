using System.Security.Cryptography.X509Certificates;
using SseApi.Services;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace SseApi.Services;

public class CertificateRenewalService : BackgroundService
{
    private readonly AcmeService _acmeService;
    private readonly ILogger<CertificateRenewalService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval;
    private readonly int _renewDays;
    
    private X509Certificate2? _currentCertificate;

    public CertificateRenewalService(
        AcmeService acmeService, 
        ILogger<CertificateRenewalService> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _acmeService = acmeService;
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        
        _checkInterval = TimeSpan.FromHours(_configuration.GetValue<int>("SslCertificate:CheckIntervalHours", 6));
        _renewDays = _configuration.GetValue<int>("SslCertificate:AutoRenewDays", 30);
    }

    public X509Certificate2? CurrentCertificate => _currentCertificate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Certificate renewal service started. Check interval: {Interval}", _checkInterval);

        // 跳过启动时的证书检查，直接进入定时检查循环
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
                await CheckAndRenewCertificateAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in certificate renewal service");
                await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken); // 错误后等待30分钟再重试
            }
        }

        _logger.LogInformation("Certificate renewal service stopped");
    }

    private async Task CheckAndRenewCertificateAsync()
    {
        try
        {
            // 首先尝试加载现有证书
            if (_currentCertificate == null)
            {
                _currentCertificate = await _acmeService.LoadExistingCertificateAsync();
            }

            // 检查证书是否需要更新
            if (!_acmeService.IsCertificateValid(_currentCertificate, _renewDays))
            {
                _logger.LogInformation("Certificate needs renewal or doesn't exist. Requesting new certificate...");

                // 定义证书目录路径
                var storePath = _configuration["SslCertificate:CertificateStorePath"] ?? "./certificates";

                if (!System.IO.Directory.Exists(storePath))
                {
                    System.IO.Directory.CreateDirectory(storePath);
                }

                var cert = await _acmeService.RequestCertificateAsync();

                if (cert != null)
                {
                    _currentCertificate = cert;
                    _logger.LogInformation("Certificate renewal completed successfully");

                    // 通知应用程序重新加载证书（如果需要的话）
                    await NotifyCertificateUpdatedAsync();
                }
                else
                {
                    _logger.LogError("Failed to renew certificate");
                }
            }
            else
            {
                _logger.LogDebug("Certificate is still valid, no renewal needed");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking/renewing certificate");
        }
    }

    private async Task NotifyCertificateUpdatedAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var connectionManager = scope.ServiceProvider.GetService<SseConnectionManager>();

            if (connectionManager != null)
            {
                await connectionManager.BroadcastAsync("certificate-renewed", new
                {
                    timestamp = DateTime.UtcNow,
                    message = "SSL certificate has been renewed",
                    validFrom = _currentCertificate?.NotBefore,
                    validTo = _currentCertificate?.NotAfter
                });

                _logger.LogInformation("Certificate renewal notification sent to SSE clients");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending certificate renewal notification");
        }
    }

    public async Task<bool> ForceCertificateRenewalAsync()
    {
        try
        {
            _logger.LogInformation("Forcing certificate renewal...");
            
            var newCertificate = await _acmeService.RequestCertificateAsync();
            if (newCertificate != null)
            {
                _currentCertificate = newCertificate;
                _logger.LogInformation("Forced certificate renewal completed successfully");
                
                await NotifyCertificateUpdatedAsync();
                return true;
            }
            else
            {
                _logger.LogError("Failed to force renew certificate");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forcing certificate renewal");
            return false;
        }
    }
}