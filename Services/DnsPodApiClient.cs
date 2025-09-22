using System.Text.Json;
using System.Text;

namespace SseApi.Services;

public class DnsPodApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiId;
    private readonly string _apiToken;
    private readonly ILogger<DnsPodApiClient> _logger;

    public DnsPodApiClient(IConfiguration configuration, ILogger<DnsPodApiClient> logger)
    {
        _httpClient = new HttpClient();
        _apiId = configuration["DnsPod:ApiId"] ?? 
                Environment.GetEnvironmentVariable("DP_Id") ?? 
                throw new ArgumentException("DnsPod API ID not configured");
        _apiToken = configuration["DnsPod:ApiToken"] ?? 
                   Environment.GetEnvironmentVariable("DP_Key") ?? 
                   throw new ArgumentException("DnsPod API Token not configured");
        _logger = logger;

        _logger.LogInformation("DnsPodApiClient initialized with API ID: {ApiId}", _apiId);
    }

    // 添加缺少的 GetDomainsAsync 方法
    public async Task<object> GetDomainsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching domains from DnsPod API");

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("login_token", $"{_apiId},{_apiToken}"),
                new KeyValuePair<string, string>("format", "json")
            });

            var response = await _httpClient.PostAsync("https://dnsapi.cn/Domain.List", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("DnsPod API response: {Response}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(responseContent);
                return jsonDoc.RootElement;
            }
            else
            {
                _logger.LogError("DnsPod API error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new { error = "API request failed", statusCode = response.StatusCode, content = responseContent };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling DnsPod API");
            throw;
        }
    }

    public async Task<string?> AddTxtRecordAsync(string domain, string recordName, string value)
    {
        try
        {
            _logger.LogInformation("Adding TXT record: {RecordName}.{Domain} = {Value}", recordName, domain, value);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("login_token", $"{_apiId},{_apiToken}"),
                new KeyValuePair<string, string>("format", "json"),
                new KeyValuePair<string, string>("domain", domain),
                new KeyValuePair<string, string>("sub_domain", recordName),
                new KeyValuePair<string, string>("record_type", "TXT"),
                new KeyValuePair<string, string>("value", value),
                new KeyValuePair<string, string>("record_line", "默认")
            });

            var response = await _httpClient.PostAsync("https://dnsapi.cn/Record.Create", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Add TXT record response: {Response}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(responseContent);
                if (jsonDoc.RootElement.TryGetProperty("status", out var status) && 
                    jsonDoc.RootElement.TryGetProperty("record", out var record) &&
                    status.TryGetProperty("code", out var code) && code.GetString() == "1")
                {
                    var recordId = record.TryGetProperty("id", out var id) ? id.GetString() : null;
                    _logger.LogInformation("TXT record added successfully with ID: {RecordId}", recordId);
                    return recordId;
                }
            }

            _logger.LogError("Failed to add TXT record: {Response}", responseContent);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding TXT record");
            return null;
        }
    }

    public async Task<bool> DeleteTxtRecordAsync(string domain, string recordId)
    {
        try
        {
            _logger.LogInformation("Deleting TXT record: {RecordId} from {Domain}", recordId, domain);

            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("login_token", $"{_apiId},{_apiToken}"),
                new KeyValuePair<string, string>("format", "json"),
                new KeyValuePair<string, string>("domain", domain),
                new KeyValuePair<string, string>("record_id", recordId)
            });

            var response = await _httpClient.PostAsync("https://dnsapi.cn/Record.Remove", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogDebug("Delete TXT record response: {Response}", responseContent);

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(responseContent);
                if (jsonDoc.RootElement.TryGetProperty("status", out var status) &&
                    status.TryGetProperty("code", out var code) && code.GetString() == "1")
                {
                    _logger.LogInformation("TXT record deleted successfully");
                    return true;
                }
            }

            _logger.LogError("Failed to delete TXT record: {Response}", responseContent);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting TXT record");
            return false;
        }
    }

    public async Task<bool> DeleteTxtRecordByNameAsync(string domain, string recordName)
    {
        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("login_token", $"{_apiId},{_apiToken}"),
                new KeyValuePair<string, string>("format", "json"),
                new KeyValuePair<string, string>("domain", domain),
                new KeyValuePair<string, string>("sub_domain", recordName)
            });

            var response = await _httpClient.PostAsync("https://dnsapi.cn/Record.List", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var jsonDoc = JsonDocument.Parse(responseContent);
                if (jsonDoc.RootElement.TryGetProperty("records", out var records))
                {
                    foreach (var record in records.EnumerateArray())
                    {
                        if (record.TryGetProperty("name", out var name) &&
                            record.TryGetProperty("type", out var type) &&
                            record.TryGetProperty("id", out var id) &&
                            name.GetString() == recordName &&
                            type.GetString() == "TXT")
                        {
                            return await DeleteTxtRecordAsync(domain, id.GetString()!);
                        }
                    }
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting TXT record by name");
            return false;
        }
    }
}