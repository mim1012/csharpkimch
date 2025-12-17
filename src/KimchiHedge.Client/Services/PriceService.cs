using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Client.Services;

/// <summary>
/// 가격 데이터
/// </summary>
public class PriceData
{
    public decimal UpbitPrice { get; set; }
    public decimal BingxPrice { get; set; }
    public decimal ExchangeRate { get; set; }
    public decimal KimchiPremium { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// 가격 서비스 인터페이스
/// </summary>
public interface IPriceService
{
    /// <summary>
    /// 현재 가격 데이터
    /// </summary>
    PriceData CurrentPrice { get; }

    /// <summary>
    /// 가격 업데이트 이벤트
    /// </summary>
    event EventHandler<PriceData>? PriceUpdated;

    /// <summary>
    /// 가격 폴링 시작
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 가격 폴링 중지
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// 즉시 가격 조회
    /// </summary>
    Task RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// 실시간 가격 서비스
/// IHttpClientFactory를 사용하여 소켓 고갈 방지
/// </summary>
public class PriceService : IPriceService, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PriceService> _logger;
    private Timer? _pollTimer;
    private bool _isRunning;
    private readonly object _lock = new();
    private CancellationTokenSource? _cts;

    private const string HttpClientName = "PriceService";
    private const string UpbitTickerUrl = "https://api.upbit.com/v1/ticker?markets=KRW-BTC";
    private const string BingxTickerUrl = "https://open-api.bingx.com/openApi/swap/v2/quote/price?symbol=BTC-USDT";
    private const string ExchangeRateUrl = "https://api.exchangerate-api.com/v4/latest/USD";
    private const int PollIntervalMs = 1000;

    public PriceData CurrentPrice { get; private set; } = new();

    public event EventHandler<PriceData>? PriceUpdated;

    public PriceService(IHttpClientFactory httpClientFactory, ILogger<PriceService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isRunning) return Task.CompletedTask;

            _isRunning = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _pollTimer = new Timer(
                async _ => await PollPricesAsync(_cts.Token),
                null,
                TimeSpan.Zero,
                TimeSpan.FromMilliseconds(PollIntervalMs));

            _logger.LogInformation("Price service started");
        }

        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        lock (_lock)
        {
            if (!_isRunning) return Task.CompletedTask;

            _isRunning = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _pollTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            _pollTimer?.Dispose();
            _pollTimer = null;

            _logger.LogInformation("Price service stopped");
        }

        return Task.CompletedTask;
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        await PollPricesAsync(cancellationToken);
    }

    private async Task PollPricesAsync(CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return;

        try
        {
            using var httpClient = _httpClientFactory.CreateClient(HttpClientName);

            var upbitTask = GetUpbitPriceAsync(httpClient, cancellationToken);
            var bingxTask = GetBingxPriceAsync(httpClient, cancellationToken);
            var exchangeRateTask = GetExchangeRateAsync(httpClient, cancellationToken);

            await Task.WhenAll(upbitTask, bingxTask, exchangeRateTask);

            var upbitPrice = await upbitTask;
            var bingxPrice = await bingxTask;
            var exchangeRate = await exchangeRateTask;

            // 김치 프리미엄 계산
            decimal kimchiPremium = 0;
            if (bingxPrice > 0 && exchangeRate > 0)
            {
                var bingxInKrw = bingxPrice * exchangeRate;
                kimchiPremium = bingxInKrw > 0
                    ? (upbitPrice - bingxInKrw) / bingxInKrw * 100
                    : 0;
            }

            CurrentPrice = new PriceData
            {
                UpbitPrice = upbitPrice,
                BingxPrice = bingxPrice,
                ExchangeRate = exchangeRate,
                KimchiPremium = Math.Round(kimchiPremium, 2),
                Timestamp = DateTime.Now
            };

            PriceUpdated?.Invoke(this, CurrentPrice);
        }
        catch (OperationCanceledException)
        {
            // 정상적인 취소
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling prices");
        }
    }

    private async Task<decimal> GetUpbitPriceAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync(UpbitTickerUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return CurrentPrice.UpbitPrice;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetArrayLength() > 0)
            {
                var ticker = root[0];
                if (ticker.TryGetProperty("trade_price", out var price))
                {
                    return price.GetDecimal();
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch Upbit price");
        }

        return CurrentPrice.UpbitPrice;
    }

    private async Task<decimal> GetBingxPriceAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetAsync(BingxTickerUrl, cancellationToken);
            if (!response.IsSuccessStatusCode) return CurrentPrice.BingxPrice;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("data", out var data) &&
                data.TryGetProperty("price", out var price))
            {
                if (decimal.TryParse(price.GetString(), out var priceValue))
                {
                    return priceValue;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch BingX price");
        }

        return CurrentPrice.BingxPrice;
    }

    private async Task<decimal> GetExchangeRateAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        // 환율은 1분마다만 업데이트 (API 제한 고려)
        if (CurrentPrice.ExchangeRate > 0 &&
            (DateTime.Now - CurrentPrice.Timestamp).TotalMinutes < 1)
        {
            return CurrentPrice.ExchangeRate;
        }

        try
        {
            var response = await httpClient.GetAsync(ExchangeRateUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return CurrentPrice.ExchangeRate > 0 ? CurrentPrice.ExchangeRate : 1350m;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("rates", out var rates) &&
                rates.TryGetProperty("KRW", out var krwRate))
            {
                return krwRate.GetDecimal();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch exchange rate");
        }

        return CurrentPrice.ExchangeRate > 0 ? CurrentPrice.ExchangeRate : 1350m;
    }

    public void Dispose()
    {
        StopAsync().Wait();
        _cts?.Dispose();
    }
}
