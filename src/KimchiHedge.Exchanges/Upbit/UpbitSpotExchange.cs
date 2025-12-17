using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using KimchiHedge.Core.Exchanges;
using KimchiHedge.Core.Models;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Exchanges.Upbit;

/// <summary>
/// 업비트 현물 거래소 구현체
/// </summary>
public class UpbitSpotExchange : ISpotExchange, IDisposable
{
    private const string BaseUrl = "https://api.upbit.com/v1";

    private readonly HttpClient _httpClient;
    private readonly UpbitAuthenticator _authenticator;
    private readonly ILogger<UpbitSpotExchange> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private bool _isConnected;

    public string Name => "Upbit";
    public bool IsConnected => _isConnected;

    public UpbitSpotExchange(
        string accessKey,
        string secretKey,
        ILogger<UpbitSpotExchange> logger)
    {
        _authenticator = new UpbitAuthenticator(accessKey, secretKey);
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true
        };
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _isConnected = true;
        _logger.LogInformation("업비트 연결 완료");
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        _logger.LogInformation("업비트 연결 해제");
        return Task.CompletedTask;
    }

    /// <summary>
    /// 자산 잔고 조회
    /// GET /v1/accounts
    /// </summary>
    public async Task<decimal> GetBalanceAsync(string asset, CancellationToken cancellationToken = default)
    {
        var token = _authenticator.CreateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/accounts");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        var accounts = await response.Content.ReadFromJsonAsync<List<UpbitAccount>>(_jsonOptions, cancellationToken);

        var account = accounts?.FirstOrDefault(a =>
            a.Currency.Equals(asset, StringComparison.OrdinalIgnoreCase));

        if (account == null)
        {
            _logger.LogDebug("{Asset} 잔고 없음", asset);
            return 0;
        }

        var balance = decimal.Parse(account.Balance);
        _logger.LogDebug("{Asset} 잔고: {Balance}", asset, balance);
        return balance;
    }

    /// <summary>
    /// 시장가 매수 (Quote 기준)
    /// POST /v1/orders
    /// side=bid, ord_type=price
    /// </summary>
    public async Task<OrderResult> PlaceMarketBuyAsync(
        string symbol,
        decimal quoteAmount,
        CancellationToken cancellationToken = default)
    {
        // BTC/KRW -> KRW-BTC
        var market = ConvertSymbolToMarket(symbol);

        var body = new Dictionary<string, string>
        {
            { "market", market },
            { "side", "bid" },
            { "ord_type", "price" },
            { "price", quoteAmount.ToString("F0") }  // KRW는 정수
        };

        return await PlaceOrderAsync(body, cancellationToken);
    }

    /// <summary>
    /// 시장가 매도 (Base 기준)
    /// POST /v1/orders
    /// side=ask, ord_type=market
    /// </summary>
    public async Task<OrderResult> PlaceMarketSellAsync(
        string symbol,
        decimal baseAmount,
        CancellationToken cancellationToken = default)
    {
        var market = ConvertSymbolToMarket(symbol);

        var body = new Dictionary<string, string>
        {
            { "market", market },
            { "side", "ask" },
            { "ord_type", "market" },
            { "volume", baseAmount.ToString("G") }
        };

        return await PlaceOrderAsync(body, cancellationToken);
    }

    /// <summary>
    /// 전량 시장가 매도
    /// </summary>
    public async Task<OrderResult> PlaceMarketSellAllAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        // 심볼에서 base 자산 추출 (BTC/KRW -> BTC)
        var baseAsset = symbol.Split('/')[0];
        var balance = await GetBalanceAsync(baseAsset, cancellationToken);

        if (balance <= 0)
        {
            _logger.LogWarning("{Asset} 잔고 없음, 매도 스킵", baseAsset);
            return new OrderResult
            {
                IsSuccess = true,
                ExecutedQuantity = 0,
                AveragePrice = 0
            };
        }

        return await PlaceMarketSellAsync(symbol, balance, cancellationToken);
    }

    /// <summary>
    /// 주문 조회
    /// GET /v1/order
    /// </summary>
    public async Task<OrderResult> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            { "uuid", orderId }
        };

        var token = _authenticator.CreateToken(queryParams);
        var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={p.Value}"));
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/order?{queryString}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        var order = await response.Content.ReadFromJsonAsync<UpbitOrder>(_jsonOptions, cancellationToken);

        if (order == null)
        {
            return OrderResult.Failure("주문 조회 실패");
        }

        return new OrderResult
        {
            IsSuccess = order.State == "done" || order.State == "cancel",
            OrderId = order.Uuid,
            ExecutedQuantity = decimal.TryParse(order.ExecutedVolume, out var vol) ? vol : 0,
            AveragePrice = CalculateAveragePrice(order),
            Fee = decimal.TryParse(order.PaidFee, out var fee) ? fee : 0
        };
    }

    /// <summary>
    /// 현재가 조회
    /// GET /v1/ticker
    /// </summary>
    public async Task<decimal> GetCurrentPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var market = ConvertSymbolToMarket(symbol);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/ticker?markets={market}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        var tickers = await response.Content.ReadFromJsonAsync<List<UpbitTicker>>(_jsonOptions, cancellationToken);
        var ticker = tickers?.FirstOrDefault();

        return ticker?.TradePrice ?? 0;
    }

    /// <summary>
    /// 체결 완료된 주문 내역 조회
    /// GET /v1/orders/closed
    /// </summary>
    public async Task<List<TradeHistory>> GetOrderHistoryAsync(
        string? symbol = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var queryParams = new Dictionary<string, string>
        {
            { "state", "done" },
            { "limit", Math.Min(limit, 100).ToString() },
            { "order_by", "desc" }
        };

        if (!string.IsNullOrEmpty(symbol))
        {
            queryParams["market"] = ConvertSymbolToMarket(symbol);
        }

        var queryString = string.Join("&", queryParams.Select(p => $"{p.Key}={p.Value}"));
        var token = _authenticator.CreateToken(queryParams);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/orders/closed?{queryString}");
        request.Headers.Add("Authorization", $"Bearer {token}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);

        var orders = await response.Content.ReadFromJsonAsync<List<UpbitClosedOrder>>(_jsonOptions, cancellationToken);

        if (orders == null)
        {
            return new List<TradeHistory>();
        }

        var result = new List<TradeHistory>();
        foreach (var order in orders)
        {
            // 시간 필터링
            if (DateTime.TryParse(order.CreatedAt, out var createdAt))
            {
                if (startTime.HasValue && createdAt < startTime.Value) continue;
                if (endTime.HasValue && createdAt > endTime.Value) continue;
            }

            var executedVolume = decimal.TryParse(order.ExecutedVolume, out var vol) ? vol : 0;
            var executedFunds = decimal.TryParse(order.ExecutedFunds, out var funds) ? funds : 0;
            var avgPrice = executedVolume > 0 ? executedFunds / executedVolume : 0;
            var fee = decimal.TryParse(order.PaidFee, out var f) ? f : 0;

            result.Add(new TradeHistory
            {
                OrderId = order.Uuid,
                Symbol = ConvertMarketToSymbol(order.Market),
                Side = order.Side == "bid" ? "Buy" : "Sell",
                OrderType = order.OrdType == "limit" ? "Limit" : "Market",
                ExecutedQuantity = executedVolume,
                AveragePrice = avgPrice,
                ExecutedAmount = executedFunds,
                Fee = fee,
                Status = order.State,
                CreatedAt = DateTime.TryParse(order.CreatedAt, out var dt) ? dt : DateTime.UtcNow,
                Exchange = "Upbit"
            });
        }

        _logger.LogInformation("업비트 거래 내역 {Count}건 조회", result.Count);
        return result;
    }

    /// <summary>
    /// 마켓 코드를 심볼로 변환 (KRW-BTC -> BTC/KRW)
    /// </summary>
    private static string ConvertMarketToSymbol(string market)
    {
        var parts = market.Split('-');
        return parts.Length == 2 ? $"{parts[1]}/{parts[0]}" : market;
    }

    /// <summary>
    /// 주문 실행 공통
    /// </summary>
    private async Task<OrderResult> PlaceOrderAsync(
        Dictionary<string, string> body,
        CancellationToken cancellationToken)
    {
        var jsonBody = JsonSerializer.Serialize(body);
        var token = _authenticator.CreateTokenWithBody(jsonBody);

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/orders")
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {token}");

        _logger.LogInformation("업비트 주문 요청: {Body}", jsonBody);

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("업비트 주문 실패: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            return OrderResult.Failure($"주문 실패: {response.StatusCode} - {errorContent}");
        }

        var order = await response.Content.ReadFromJsonAsync<UpbitOrder>(_jsonOptions, cancellationToken);

        if (order == null)
        {
            return OrderResult.Failure("주문 응답 파싱 실패");
        }

        _logger.LogInformation("업비트 주문 성공: {OrderId}", order.Uuid);

        // 체결 대기 후 결과 조회
        await Task.Delay(500, cancellationToken);
        return await GetOrderAsync(order.Uuid, cancellationToken);
    }

    /// <summary>
    /// 심볼 변환 (BTC/KRW -> KRW-BTC)
    /// </summary>
    private static string ConvertSymbolToMarket(string symbol)
    {
        var parts = symbol.Split('/');
        return $"{parts[1]}-{parts[0]}";
    }

    /// <summary>
    /// 평균 체결가 계산
    /// </summary>
    private static decimal CalculateAveragePrice(UpbitOrder order)
    {
        if (order.Trades == null || order.Trades.Count == 0)
        {
            return 0;
        }

        var totalVolume = order.Trades.Sum(t => decimal.Parse(t.Volume));
        var totalAmount = order.Trades.Sum(t => decimal.Parse(t.Volume) * decimal.Parse(t.Price));

        return totalVolume > 0 ? totalAmount / totalVolume : 0;
    }

    /// <summary>
    /// 응답 상태 확인
    /// </summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            _logger.LogError("업비트 API 오류: {StatusCode} - {Content}",
                response.StatusCode, content);
            throw new HttpRequestException($"업비트 API 오류: {response.StatusCode} - {content}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

#region Response Models

internal class UpbitAccount
{
    public string Currency { get; set; } = string.Empty;
    public string Balance { get; set; } = "0";
    public string Locked { get; set; } = "0";
    [JsonPropertyName("avg_buy_price")]
    public string AvgBuyPrice { get; set; } = "0";
}

internal class UpbitOrder
{
    public string Uuid { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    [JsonPropertyName("ord_type")]
    public string OrdType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Volume { get; set; } = "0";
    [JsonPropertyName("remaining_volume")]
    public string RemainingVolume { get; set; } = "0";
    [JsonPropertyName("executed_volume")]
    public string ExecutedVolume { get; set; } = "0";
    [JsonPropertyName("paid_fee")]
    public string PaidFee { get; set; } = "0";
    public List<UpbitTrade>? Trades { get; set; }
}

internal class UpbitTrade
{
    public string Market { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string Price { get; set; } = "0";
    public string Volume { get; set; } = "0";
    public string Funds { get; set; } = "0";
    public string Side { get; set; } = string.Empty;
}

internal class UpbitTicker
{
    public string Market { get; set; } = string.Empty;
    [JsonPropertyName("trade_price")]
    public decimal TradePrice { get; set; }
}

internal class UpbitClosedOrder
{
    public string Uuid { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    [JsonPropertyName("ord_type")]
    public string OrdType { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Volume { get; set; } = "0";
    [JsonPropertyName("remaining_volume")]
    public string RemainingVolume { get; set; } = "0";
    [JsonPropertyName("executed_volume")]
    public string ExecutedVolume { get; set; } = "0";
    [JsonPropertyName("executed_funds")]
    public string ExecutedFunds { get; set; } = "0";
    [JsonPropertyName("paid_fee")]
    public string PaidFee { get; set; } = "0";
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; set; } = string.Empty;
    [JsonPropertyName("trades_count")]
    public int TradesCount { get; set; }
}

#endregion
