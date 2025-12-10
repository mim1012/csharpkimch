using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using KimchiHedge.Core.Auth.Models;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Client.Services.Http;

/// <summary>
/// API 클라이언트 - HTTP 통신 담당
/// </summary>
public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApiClient(HttpClient httpClient, ILogger<ApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    /// <summary>
    /// POST 요청
    /// </summary>
    public async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? payload, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("POST {Endpoint}", endpoint);

            var response = await _httpClient.PostAsJsonAsync(endpoint, payload, _jsonOptions, ct);
            return await HandleResponseAsync<T>(response, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed: {Endpoint}", endpoint);
            return ApiResponse<T>.Fail("NETWORK_ERROR", "네트워크 연결을 확인해주세요.");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Request timeout: {Endpoint}", endpoint);
            return ApiResponse<T>.Fail("TIMEOUT", "요청 시간이 초과되었습니다.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error: {Endpoint}", endpoint);
            return ApiResponse<T>.Fail("UNKNOWN_ERROR", "알 수 없는 오류가 발생했습니다.");
        }
    }

    /// <summary>
    /// GET 요청
    /// </summary>
    public async Task<ApiResponse<T>> GetAsync<T>(string endpoint, CancellationToken ct = default)
    {
        try
        {
            _logger.LogDebug("GET {Endpoint}", endpoint);

            var response = await _httpClient.GetAsync(endpoint, ct);
            return await HandleResponseAsync<T>(response, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed: {Endpoint}", endpoint);
            return ApiResponse<T>.Fail("NETWORK_ERROR", "네트워크 연결을 확인해주세요.");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Request timeout: {Endpoint}", endpoint);
            return ApiResponse<T>.Fail("TIMEOUT", "요청 시간이 초과되었습니다.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error: {Endpoint}", endpoint);
            return ApiResponse<T>.Fail("UNKNOWN_ERROR", "알 수 없는 오류가 발생했습니다.");
        }
    }

    private async Task<ApiResponse<T>> HandleResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrEmpty(content))
        {
            if (response.IsSuccessStatusCode)
            {
                return ApiResponse<T>.Ok(default);
            }
            return ApiResponse<T>.Fail("EMPTY_RESPONSE", $"빈 응답 (Status: {response.StatusCode})");
        }

        try
        {
            var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(content, _jsonOptions);
            return apiResponse ?? ApiResponse<T>.Fail("PARSE_ERROR", "응답 파싱 실패");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse response: {Content}", content);
            return ApiResponse<T>.Fail("PARSE_ERROR", "응답 형식이 올바르지 않습니다.");
        }
    }
}
