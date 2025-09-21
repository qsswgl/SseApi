using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace SseApi.Services;

public class DnsPodApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiId;
    private readonly string _apiToken;
    private readonly ILogger<DnsPodApiClient> _logger;

    public DnsPodApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<DnsPodApiClient> logger)
    {
        _httpClient = httpClient;
        _apiId = configuration["DnsPod:ApiId"] ?? Environment.GetEnvironmentVariable("DP_Id") ?? throw new ArgumentException("DnsPod API ID not configured");
        _apiToken = configuration["DnsPod:ApiToken"] ?? Environment.GetEnvironmentVariable("DP_Key") ?? throw new ArgumentException("DnsPod API Token not configured");
        _logger = logger;
        
        _httpClient.BaseAddress = new Uri("https://dnsapi.cn/");
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SseApi/1.0.0");
    }

    public async Task<string?> AddTxtRecordAsync(string domain, string subdomain, string value)
    {
        try
        {
            var domainInfo = await GetDomainInfoAsync(domain);
            if (domainInfo == null)
            {
                _logger.LogError("Failed to get domain info for {Domain}", domain);
                return null;
            }

            var parameters = new Dictionary<string, string>
            {
                { "domain_id", domainInfo.Id.ToString() },
                { "sub_domain", subdomain },
                { "record_type", "TXT" },
                { "value", value },
                { "record_line", "默认" }
            };

            var response = await PostAsync("Record.Create", parameters);
            if (response?.Status?.Code == "1")
            {
                _logger.LogInformation("Successfully added TXT record for {Subdomain}.{Domain}", subdomain, domain);
                return response.Record?.Id.ToString();
            }
            
            _logger.LogError("Failed to add TXT record: {Message}", response?.Status?.Message);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding TXT record for {Subdomain}.{Domain}", subdomain, domain);
            return null;
        }
    }

    public async Task<bool> DeleteTxtRecordAsync(string domain, string recordId)
    {
        try
        {
            var domainInfo = await GetDomainInfoAsync(domain);
            if (domainInfo == null)
            {
                _logger.LogError("Failed to get domain info for {Domain}", domain);
                return false;
            }

            var parameters = new Dictionary<string, string>
            {
                { "domain_id", domainInfo.Id.ToString() },
                { "record_id", recordId }
            };

            var response = await PostAsync("Record.Remove", parameters);
            if (response?.Status?.Code == "1")
            {
                _logger.LogInformation("Successfully deleted TXT record {RecordId} for {Domain}", recordId, domain);
                return true;
            }
            
            _logger.LogError("Failed to delete TXT record: {Message}", response?.Status?.Message);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting TXT record {RecordId} for {Domain}", recordId, domain);
            return false;
        }
    }

    private async Task<DomainInfo?> GetDomainInfoAsync(string domain)
    {
        try
        {
            var parameters = new Dictionary<string, string>
            {
                { "type", "all" },
                { "keyword", domain }
            };

            var response = await PostAsync("Domain.List", parameters);
            var domainInfo = response?.Domains?.FirstOrDefault(d => d.Name == domain);
            
            if (domainInfo == null)
            {
                _logger.LogError("Domain {Domain} not found in DnsPod account", domain);
            }
            
            return domainInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting domain info for {Domain}", domain);
            return null;
        }
    }

    private async Task<DnsPodResponse?> PostAsync(string action, Dictionary<string, string> parameters)
    {
        // 使用正确的 DNSPOD API 认证格式
        parameters["login_token"] = $"{_apiId},{_apiToken}";
        parameters["format"] = "json";
        parameters["lang"] = "cn";
        
        _logger.LogDebug("Calling DNSPod API: {Action} with login_token: {ApiId},****", 
            action, _apiId);
            
        var content = new FormUrlEncodedContent(parameters);
        
        // 确保 Content-Type 正确
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");
        
        var response = await _httpClient.PostAsync(action, content);
        
        var jsonResponse = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("DnsPod API response status: {StatusCode}, body: {Response}", 
            response.StatusCode, jsonResponse);

        // 检查是否返回了 HTML 错误页面而不是 JSON
        if (jsonResponse.TrimStart().StartsWith("<"))
        {
            _logger.LogError("DNSPod API returned HTML error page instead of JSON: {Response}", jsonResponse);
            return null;
        }

        DnsPodResponse? dnsPodResponse = null;
        try
        {
            dnsPodResponse = JsonSerializer.Deserialize<DnsPodResponse>(jsonResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse DNSPod API response as JSON: {Response}", jsonResponse);
            return null;
        }

        // 检查 DNSPOD 的业务状态码
        if (dnsPodResponse?.Status?.Code != "1")
        {
            _logger.LogError("DNSPod API business error: {Code} - {Message}", 
                dnsPodResponse?.Status?.Code, dnsPodResponse?.Status?.Message);
            return null;
        }

        return dnsPodResponse;
    }
}

public class DnsPodResponse
{
    public DnsPodStatus? Status { get; set; }
    public List<DomainInfo>? Domains { get; set; }
    public RecordInfo? Record { get; set; }
}

public class DnsPodStatus
{
    public string? Code { get; set; }
    public string? Message { get; set; }
}

public class DomainInfo
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Status { get; set; }
}

public class RecordInfo
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? Value { get; set; }
}