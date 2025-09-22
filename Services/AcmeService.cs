using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using System.Security.Cryptography.X509Certificates;

namespace SseApi.Services
{
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
            _domain = configuration["SslCertificate:Domain"] ?? "qsgl.net";
            _email = configuration["SslCertificate:Email"] ?? "admin@qsgl.net";

            System.IO.Directory.CreateDirectory(_certificateStorePath);
        }

        // 兼容旧调用：尝试申请证书后再加载返回
        public async Task<X509Certificate2?> RequestCertificateAsync()
        {
            try
            {
                await ObtainCertificateAsync();

                var pfxPath = System.IO.Path.Combine(_certificateStorePath, $"{_domain}.pfx");
                if (!System.IO.File.Exists(pfxPath))
                {
                    _logger.LogWarning("证书文件不存在: {Path}", pfxPath);
                    return null;
                }

                var pfxBytes = await System.IO.File.ReadAllBytesAsync(pfxPath);
                var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, "");
                _logger.LogInformation("已加载证书: {Subject}, NotAfter={NotAfter}", cert.Subject, cert.NotAfter);
                return cert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RequestCertificateAsync 失败");
                return null;
            }
        }

        // 从磁盘加载现有证书
        public async Task<X509Certificate2?> LoadExistingCertificateAsync()
        {
            try
            {
                var pfxPath = System.IO.Path.Combine(_certificateStorePath, $"{_domain}.pfx");
                if (!System.IO.File.Exists(pfxPath))
                {
                    _logger.LogInformation("未找到现有证书: {Path}", pfxPath);
                    return null;
                }

                var pfxBytes = await System.IO.File.ReadAllBytesAsync(pfxPath);
                var cert = X509CertificateLoader.LoadPkcs12(pfxBytes, "");
                _logger.LogInformation("已加载现有证书: {Subject}, NotAfter={NotAfter}", cert.Subject, cert.NotAfter);
                return cert;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载现有证书失败");
                return null;
            }
        }

        // 判断证书是否仍然有效（距离过期 > renewDays）
        public bool IsCertificateValid(X509Certificate2? certificate, int renewDays = 30)
        {
            if (certificate == null) return false;
            var renewDate = DateTime.UtcNow.AddDays(renewDays);
            var valid = certificate.NotAfter.ToUniversalTime() > renewDate;
            _logger.LogInformation("证书有效性: NotAfter={NotAfter}, 阈值={RenewDate}, 仍有效={Valid}",
                certificate.NotAfter, renewDate, valid);
            return valid;
        }

        public async Task ObtainCertificateAsync()
        {
            // 使用 Let's Encrypt V2，执行 DNS-01 验证，申请 *.domain 与根域名证书
            var identifiers = new[] { $"*.{_domain}", _domain };
            var createdRecordIds = new List<string>();

            try
            {
                _logger.LogInformation("开始 ACME 申请: {Identifiers}", string.Join(",", identifiers));

                var acme = new AcmeContext(WellKnownServers.LetsEncryptV2);
                await acme.NewAccount(_email, true);

                var order = await acme.NewOrder(identifiers);
                var authzs = await order.Authorizations();

                // 为每个授权创建对应的 TXT 记录
                foreach (var authz in authzs)
                {
                    var authzRes = await authz.Resource();
                    var idValue = authzRes.Identifier.Value; // 可能是 *.qsgl.net 或 qsgl.net
                    var dnsChallenge = await authz.Dns();
                    var dnsTxt = acme.AccountKey.DnsTxt(dnsChallenge.Token);

                    var recordName = GetDnsChallengeRecordName(idValue);
                    var recordId = await _dnsClient.AddTxtRecordAsync(_domain, recordName, dnsTxt);
                    if (!string.IsNullOrEmpty(recordId))
                    {
                        createdRecordIds.Add(recordId);
                        _logger.LogInformation("已创建 TXT 记录: {RecordName}.{Domain} -> {RecordId}", recordName, _domain, recordId);
                    }
                    else
                    {
                        throw new InvalidOperationException($"创建 TXT 记录失败: {recordName}.{_domain}");
                    }
                }

                // 等待 DNS 生效并验证，每个挑战轮询一段时间
                foreach (var authz in authzs)
                {
                    var authzRes = await authz.Resource();
                    var idValue = authzRes.Identifier.Value;
                    var challenge = await authz.Dns();
                    var success = false;
                    for (var i = 0; i < 30; i++) // 最多约5分钟
                    {
                        try { await challenge.Validate(); } catch { /* 忽略瞬时错误 */ }

                        var resource = await challenge.Resource();
                        if (resource.Status == ChallengeStatus.Valid)
                        {
                            success = true;
                            break;
                        }
                        else if (resource.Status == ChallengeStatus.Invalid)
                        {
                            throw new InvalidOperationException($"DNS 验证失败: {idValue}");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(10));
                    }

                    if (!success)
                    {
                        throw new TimeoutException($"DNS 验证超时: {idValue}");
                    }
                }

                // 生成证书（ES256 私钥）
                var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);
                var certChain = await order.Generate(new CsrInfo
                {
                    CommonName = _domain,
                }, privateKey);

                var pfxBuilder = certChain.ToPfx(privateKey);
                var pfxBytes = pfxBuilder.Build(_domain, "");
                var pfxPath = System.IO.Path.Combine(_certificateStorePath, $"{_domain}.pfx");
                await System.IO.File.WriteAllBytesAsync(pfxPath, pfxBytes);

                _logger.LogInformation("证书已保存: {Path}", pfxPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ACME 申请失败");
                throw;
            }
            finally
            {
                // 清理 TXT 记录
                foreach (var rid in createdRecordIds)
                {
                    try
                    {
                        await _dnsClient.DeleteTxtRecordAsync(_domain, rid);
                    }
                    catch (Exception dex)
                    {
                        _logger.LogWarning(dex, "清理 TXT 记录失败: {RecordId}", rid);
                    }
                }
            }
        }

        private string GetDnsChallengeRecordName(string identifier)
        {
            // ACME DNS-01: _acme-challenge.[left-part]
            // 示例：
            //  - qsgl.net => _acme-challenge
            //  - *.qsgl.net => _acme-challenge
            //  - www.qsgl.net => _acme-challenge.www
            var host = identifier.TrimEnd('.');
            if (host.Equals(_domain, StringComparison.OrdinalIgnoreCase))
                return "_acme-challenge";

            var wildcardPrefix = "*.";
            if (host.StartsWith(wildcardPrefix, StringComparison.Ordinal))
            {
                host = host.Substring(wildcardPrefix.Length);
            }

            if (host.EndsWith($".{_domain}", StringComparison.OrdinalIgnoreCase))
            {
                var left = host[..^("." + _domain).Length];
                if (string.IsNullOrWhiteSpace(left) || left == "*")
                    return "_acme-challenge";
                return $"_acme-challenge.{left}";
            }

            // 回退：默认根域名
            return "_acme-challenge";
        }
    }
}