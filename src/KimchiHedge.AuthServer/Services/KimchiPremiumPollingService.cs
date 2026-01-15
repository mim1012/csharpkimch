using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KimchiHedge.AuthServer.Hubs;
using KimchiHedge.AuthServer.Models;

namespace KimchiHedge.AuthServer.Services;

/// <summary>
/// AWS Lambda API를 주기적으로 polling하여 김치프리미엄 데이터를 수신하고 브로드캐스트
/// </summary>
public class KimchiPremiumPollingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<KimchiPremiumPollingService> _logger;
    private readonly HttpClient _httpClient;

    // 최신 데이터 저장 (모니터링용)
    private static KimchiPremiumData? _latestData;
    private static LambdaApiResponse? _latestRawData;
    private static readonly object _lock = new();

    /// <summary>
    /// 최신 김치프리미엄 데이터 조회
    /// </summary>
    public static KimchiPremiumData? GetLatestData()
    {
        lock (_lock)
        {
            return _latestData;
        }
    }

    /// <summary>
    /// 최신 Lambda API 원본 응답 조회
    /// </summary>
    public static LambdaApiResponse? GetLatestRawData()
    {
        lock (_lock)
        {
            return _latestRawData;
        }
    }

    public KimchiPremiumPollingService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<KimchiPremiumPollingService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("LambdaApi");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lambdaApiUrl = _configuration["KimchiPremium:LambdaApiUrl"];
        var pollingIntervalMs = _configuration.GetValue<int>("KimchiPremium:PollingIntervalMs", 1000);
        var enabled = _configuration.GetValue<bool>("KimchiPremium:PollingEnabled", true);

        if (!enabled)
        {
            _logger.LogInformation("Kimchi Premium polling is disabled");
            return;
        }

        if (string.IsNullOrEmpty(lambdaApiUrl))
        {
            _logger.LogWarning("Lambda API URL is not configured. Polling disabled.");
            return;
        }

        _logger.LogInformation(
            "Starting Kimchi Premium polling service. URL: {Url}, Interval: {Interval}ms",
            lambdaApiUrl, pollingIntervalMs);

        await Task.Delay(3000, stoppingToken); // 초기 지연 (서버 시작 대기)

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAndBroadcastAsync(lambdaApiUrl, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error polling Lambda API");
            }

            try
            {
                await Task.Delay(pollingIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Kimchi Premium polling service stopped");
    }

    private async Task PollAndBroadcastAsync(string lambdaApiUrl, CancellationToken ct)
    {
        var response = await _httpClient.GetFromJsonAsync<LambdaApiResponse>(lambdaApiUrl, ct);

        if (response == null || response.Status != "success")
        {
            _logger.LogWarning("Invalid response from Lambda API: {Error}", response?.Error);
            return;
        }

        var kimchiData = new KimchiPremiumData
        {
            Kimchi = response.KimchiPremiumPercent,
            Upbit = response.UpbitBtcKrw,
            Global = response.BingxBtcUsd,
            Timestamp = new DateTimeOffset(response.Timestamp).ToUnixTimeSeconds(),
            ReceivedAt = DateTime.UtcNow
        };

        // 최신 데이터 저장
        lock (_lock)
        {
            _latestData = kimchiData;
            _latestRawData = response;
        }

        _logger.LogDebug(
            "Polled kimchi premium: {Kimchi}%, Upbit: {Upbit:N0}, BingX: {BingX:N2}",
            kimchiData.Kimchi, kimchiData.Upbit, kimchiData.Global);

        // SignalR로 브로드캐스트
        using var scope = _serviceProvider.CreateScope();
        var broadcaster = scope.ServiceProvider.GetRequiredService<KimchiPremiumBroadcaster>();
        await broadcaster.BroadcastToAllAsync(kimchiData);
    }
}

/// <summary>
/// Lambda API 응답 모델
/// </summary>
public class LambdaApiResponse
{
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("upbitBtcKrw")]
    public decimal UpbitBtcKrw { get; set; }

    [JsonPropertyName("bingxBtcUsd")]
    public decimal BingxBtcUsd { get; set; }

    [JsonPropertyName("exchangeRateUsdKrw")]
    public decimal ExchangeRateUsdKrw { get; set; }

    [JsonPropertyName("bingxBtcKrw")]
    public decimal BingxBtcKrw { get; set; }

    [JsonPropertyName("kimchiPremiumPercent")]
    public decimal KimchiPremiumPercent { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}
