using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using System.Security.Cryptography.X509Certificates;
using SseApi.Services;

namespace SseApi.Services;

public class AcmeService
{
    private readonly DnsPodApiClient _dnsClient;
    private readonly ILogger<AcmeService> _logger;
    private readonly string _certificateStorePath;
    private readonly string _domain;
    private readonly string _email;

    public AcmeService(DnsPodApiClient dnsClient, IConfiguration configuration, ILogger<AcmeService> logger)
    {
        _dnsClient = dnsClient;
        _logger = logger;
        _certificateStorePath = configuration["SslCertificate:CertificateStorePath"] ?? "./certificates";
        _domain = configuration["SslCertificate:Domain"] ?? throw new ArgumentException("Domain not configured");
        _email = configuration["SslCertificate:Email"] ?? throw new ArgumentException("Email not configured");

        // 确保证书存储目录存在
        System.IO.Directory.CreateDirectory(_certificateStorePath);
    }

    public async Task<X509Certificate2?> RequestCertificateAsync()
    {
        try
        {
            _logger.LogInformation("Starting certificate request for domain {Domain}", _domain);

            // 创建 ACME 上下文
            var acme = new AcmeContext(WellKnownServers.LetsEncryptV2);
            
            // 创建账户（如果不存在）
            var account = await acme.NewAccount(_email, true);
            _logger.LogInformation("ACME account created/retrieved for {Email}", _email);

            // 创建订单（泛域名证书）
            var order = await acme.NewOrder(new[] { _domain, $"*.{_domain}" });
            _logger.LogInformation("ACME order created for {Domain} and *.{Domain}", _domain, _domain);

            // 处理所有授权
            var authzList = await order.Authorizations();
            foreach (var authz in authzList)
            {
                var authzResource = await authz.Resource();
                var domain = authzResource.Identifier.Value;
                _logger.LogInformation("Processing authorization for {Domain}", domain);

                // 获取 DNS-01 挑战
                var dnsChallenge = await authz.Dns();
                var dnsTxt = acme.AccountKey.DnsTxt(dnsChallenge.Token);

                // 添加 DNS TXT 记录
                var challengeDomain = domain.StartsWith("*.") ? domain.Substring(2) : domain;
                var recordId = await _dnsClient.AddTxtRecordAsync(challengeDomain, "_acme-challenge", dnsTxt);
                
                if (recordId == null)
                {
                    _logger.LogError("Failed to add DNS TXT record for {Domain}", domain);
                    return null;
                }

                _logger.LogInformation("DNS TXT record added for {Domain}, waiting for DNS propagation...", domain);
                
                // 等待 DNS 传播
                await Task.Delay(TimeSpan.FromMinutes(2));

                try
                {
                    // 验证挑战
                    await dnsChallenge.Validate();
                    _logger.LogInformation("DNS challenge validated for {Domain}", domain);

                    // 等待验证完成
                    var authzStatus = await authz.Resource();
                    var maxWaitTime = TimeSpan.FromMinutes(5);
                    var startTime = DateTime.UtcNow;

                    while (authzStatus.Status != AuthorizationStatus.Valid && 
                           authzStatus.Status != AuthorizationStatus.Invalid &&
                           DateTime.UtcNow - startTime < maxWaitTime)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(10));
                        authzStatus = await authz.Resource();
                        _logger.LogDebug("Authorization status for {Domain}: {Status}", domain, authzStatus.Status);
                    }

                    if (authzStatus.Status != AuthorizationStatus.Valid)
                    {
                        _logger.LogError("Authorization failed for {Domain} with status {Status}", domain, authzStatus.Status);
                        return null;
                    }
                }
                finally
                {
                    // 清理 DNS 记录
                    await _dnsClient.DeleteTxtRecordAsync(challengeDomain, recordId);
                    _logger.LogInformation("DNS TXT record cleanup completed for {Domain}", domain);
                }
            }

            // 生成证书
            _logger.LogInformation("All authorizations completed, generating certificate...");
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
            var cert = await order.Generate(new CsrInfo
            {
                CountryName = "CN",
                State = "Beijing",
                Locality = "Beijing",
                Organization = "Personal",
                OrganizationUnit = "IT",
                CommonName = _domain
            }, privateKey);

            // 保存证书
            var certPath = Path.Combine(_certificateStorePath, $"{_domain}.pfx");
            var pfxPassword = GenerateRandomPassword();
            var pfxBytes = cert.ToPfx(privateKey).Build(_domain, pfxPassword);
            
            await File.WriteAllBytesAsync(certPath, pfxBytes);
            await File.WriteAllTextAsync(Path.Combine(_certificateStorePath, $"{_domain}.password"), pfxPassword);
            
            _logger.LogInformation("Certificate saved to {CertPath}", certPath);

            // 加载并返回证书
            var certificate = X509CertificateLoader.LoadPkcs12(pfxBytes, pfxPassword);
            _logger.LogInformation("Certificate requested successfully. Valid from {NotBefore} to {NotAfter}", 
                certificate.NotBefore, certificate.NotAfter);

            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting certificate for {Domain}", _domain);
            return null;
        }
    }

    public async Task<X509Certificate2?> LoadExistingCertificateAsync()
    {
        try
        {
            var certPath = Path.Combine(_certificateStorePath, $"{_domain}.pfx");
            var passwordPath = Path.Combine(_certificateStorePath, $"{_domain}.password");

            if (!File.Exists(certPath) || !File.Exists(passwordPath))
            {
                _logger.LogInformation("No existing certificate found for {Domain}", _domain);
                return null;
            }

            var password = await File.ReadAllTextAsync(passwordPath);
            var pfxBytes = await File.ReadAllBytesAsync(certPath);
            var certificate = X509CertificateLoader.LoadPkcs12(pfxBytes, password);

            _logger.LogInformation("Loaded existing certificate for {Domain}. Valid from {NotBefore} to {NotAfter}",
                _domain, certificate.NotBefore, certificate.NotAfter);

            return certificate;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading existing certificate for {Domain}", _domain);
            return null;
        }
    }

    public bool IsCertificateValid(X509Certificate2? certificate, int renewDays = 30)
    {
        if (certificate == null)
            return false;

        var renewDate = DateTime.UtcNow.AddDays(renewDays);
        var isValid = certificate.NotAfter > renewDate;

        _logger.LogInformation("Certificate validity check for {Domain}: expires {NotAfter}, renew threshold {RenewDate}, valid: {IsValid}",
            _domain, certificate.NotAfter, renewDate, isValid);

        return isValid;
    }

    private static string GenerateRandomPassword(int length = 32)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}