using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KimchiHedge.Core.Exchanges;
using KimchiHedge.Core.Models;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Exchanges.BingX;

/// <summary>
/// BingX 무기한 선물 거래소 구현체
/// </summary>
public class BingXFuturesExchange : IFuturesExchange, IDisposable
{
    private const string BaseUrl = "https://open-api.bingx.com";
    private const string SwapApiPath = "/openApi/swap/v2";

    private readonly HttpClient _httpClient;
    private readonly BingXAuthenticator _authenticator;
    private readonly ILogger<BingXFuturesExchange> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private bool _isConnected;

    public string Name => "BingX";
    public bool IsConnected => _isConnected;

    public BingXFuturesExchange(
        string apiKey,
        string secretKey,
        ILogger<BingXFuturesExchange> logger)
    {
        _authenticator = new BingXAuthenticator(apiKey, secretKey);
        _logger = logger;
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(10)
        };

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };
    }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        _isConnected = true;
        _logger.LogInformation("BingX 연결 완료");
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _isConnected = false;
        _logger.LogInformation("BingX 연결 해제");
        return Task.CompletedTask;
    }

    /// <summary>
    /// USDT 잔고 조회
    /// GET /openApi/swap/v2/user/balance
    /// </summary>
    public async Task<decimal> GetBalanceAsync(CancellationToken cancellationToken = default)
    {
        var queryString = _authenticator.CreateSignedQueryString();
        var request = CreateRequest(HttpMethod.Get, $"{SwapApiPath}/user/balance?{queryString}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await ParseResponseAsync<BingXBalanceResponse>(response, cancellationToken);

        if (result?.Data?.Balance == null)
        {
            _logger.LogWarning("BingX 잔고 조회 실패");
            return 0;
        }

        var balance = result.Data.Balance.GetAvailableMargin();
        _logger.LogDebug("BingX USDT 잔고: {Balance}", balance);
        return balance;
    }

    /// <summary>
    /// 포지션 조회
    /// GET /openApi/swap/v2/user/positions
    /// </summary>
    public async Task<FuturesPosition?> GetPositionAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            { "symbol", symbol }
        };
        var queryString = _authenticator.CreateSignedQueryString(parameters);
        var request = CreateRequest(HttpMethod.Get, $"{SwapApiPath}/user/positions?{queryString}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await ParseResponseAsync<BingXPositionsResponse>(response, cancellationToken);

        var position = result?.Data?.FirstOrDefault(p =>
            p.Symbol == symbol && p.PositionAmt != 0);

        if (position == null)
        {
            _logger.LogDebug("{Symbol} 포지션 없음", symbol);
            return null;
        }

        _logger.LogDebug("{Symbol} 포지션: {Side} {Qty} @ {Price}",
            symbol, position.PositionSide, Math.Abs(position.PositionAmt), position.AvgPrice);

        return new FuturesPosition
        {
            Symbol = position.Symbol,
            Side = position.PositionSide,
            Quantity = Math.Abs(position.PositionAmt),
            EntryPrice = position.AvgPrice,
            UnrealizedPnL = position.UnrealizedProfit,
            Leverage = position.Leverage,
            LiquidationPrice = position.LiquidationPrice
        };
    }

    /// <summary>
    /// 레버리지 설정
    /// POST /openApi/swap/v2/trade/leverage
    /// </summary>
    public async Task SetLeverageAsync(
        string symbol,
        int leverage,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            { "symbol", symbol },
            { "side", "BOTH" },
            { "leverage", leverage.ToString() }
        };
        var queryString = _authenticator.CreateSignedQueryString(parameters);
        var request = CreateRequest(HttpMethod.Post, $"{SwapApiPath}/trade/leverage?{queryString}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        _logger.LogInformation("{Symbol} 레버리지 설정: {Leverage}x", symbol, leverage);
    }

    /// <summary>
    /// 숏 포지션 오픈 (시장가)
    /// POST /openApi/swap/v2/trade/order
    /// </summary>
    public async Task<OrderResult> OpenShortAsync(
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        return await PlaceOrderAsync(
            symbol,
            "SELL",
            "SHORT",
            quantity,
            cancellationToken);
    }

    /// <summary>
    /// 롱 포지션 오픈 (시장가)
    /// POST /openApi/swap/v2/trade/order
    /// </summary>
    public async Task<OrderResult> OpenLongAsync(
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        return await PlaceOrderAsync(
            symbol,
            "BUY",
            "LONG",
            quantity,
            cancellationToken);
    }

    /// <summary>
    /// 포지션 전량 청산 (시장가)
    /// POST /openApi/swap/v2/trade/closeAllPositions
    /// </summary>
    public async Task<OrderResult> ClosePositionAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        // 현재 포지션 확인
        var position = await GetPositionAsync(symbol, cancellationToken);

        if (position == null || position.Quantity == 0)
        {
            _logger.LogWarning("{Symbol} 청산할 포지션 없음", symbol);
            return new OrderResult
            {
                IsSuccess = true,
                ExecutedQuantity = 0
            };
        }

        // 반대 방향으로 시장가 주문
        var side = position.Side == "SHORT" ? "BUY" : "SELL";
        var positionSide = position.Side;

        return await PlaceOrderAsync(
            symbol,
            side,
            positionSide,
            position.Quantity,
            cancellationToken);
    }

    /// <summary>
    /// 주문 조회
    /// GET /openApi/swap/v2/trade/order
    /// </summary>
    public async Task<OrderResult> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            { "orderId", orderId }
        };
        var queryString = _authenticator.CreateSignedQueryString(parameters);
        var request = CreateRequest(HttpMethod.Get, $"{SwapApiPath}/trade/order?{queryString}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await ParseResponseAsync<BingXOrderResponse>(response, cancellationToken);

        if (result?.Data?.Order == null)
        {
            return OrderResult.Failure("주문 조회 실패");
        }

        var order = result.Data.Order;
        return new OrderResult
        {
            IsSuccess = order.Status == "FILLED",
            OrderId = order.OrderId.ToString(),
            ExecutedQuantity = order.ExecutedQty,
            AveragePrice = order.AvgPrice,
            Fee = order.Commission
        };
    }

    /// <summary>
    /// 현재가 조회
    /// GET /openApi/swap/v2/quote/price
    /// </summary>
    public async Task<decimal> GetCurrentPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            { "symbol", symbol }
        };
        var queryString = _authenticator.CreateSignedQueryString(parameters);
        var request = CreateRequest(HttpMethod.Get, $"{SwapApiPath}/quote/price?{queryString}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await ParseResponseAsync<BingXPriceResponse>(response, cancellationToken);

        return result?.Data?.Price ?? 0;
    }

    /// <summary>
    /// 체결 완료된 주문 내역 조회
    /// GET /openApi/swap/v2/trade/allOrders
    /// </summary>
    public async Task<List<TradeHistory>> GetOrderHistoryAsync(
        string? symbol = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var parameters = new Dictionary<string, string>
        {
            { "limit", Math.Min(limit, 500).ToString() }
        };

        if (!string.IsNullOrEmpty(symbol))
        {
            parameters["symbol"] = symbol;
        }

        if (startTime.HasValue)
        {
            parameters["startTime"] = new DateTimeOffset(startTime.Value).ToUnixTimeMilliseconds().ToString();
        }

        if (endTime.HasValue)
        {
            parameters["endTime"] = new DateTimeOffset(endTime.Value).ToUnixTimeMilliseconds().ToString();
        }

        var queryString = _authenticator.CreateSignedQueryString(parameters);
        var request = CreateRequest(HttpMethod.Get, $"{SwapApiPath}/trade/allOrders?{queryString}");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await ParseResponseAsync<BingXAllOrdersResponse>(response, cancellationToken);

        if (result?.Data?.Orders == null)
        {
            return new List<TradeHistory>();
        }

        var histories = new List<TradeHistory>();
        foreach (var order in result.Data.Orders)
        {
            // 체결 완료된 주문만 필터링
            if (order.Status != "FILLED") continue;

            histories.Add(new TradeHistory
            {
                OrderId = order.OrderId.ToString(),
                Symbol = order.Symbol,
                Side = order.Side == "BUY" ? "Buy" : "Sell",
                OrderType = order.Type == "MARKET" ? "Market" : "Limit",
                ExecutedQuantity = order.ExecutedQty,
                AveragePrice = order.AvgPrice,
                ExecutedAmount = order.ExecutedQty * order.AvgPrice,
                Fee = order.Commission,
                Status = order.Status,
                CreatedAt = DateTimeOffset.FromUnixTimeMilliseconds(order.Time).DateTime,
                Exchange = "BingX",
                RealizedPnL = order.Profit
            });
        }

        _logger.LogInformation("BingX 거래 내역 {Count}건 조회", histories.Count);
        return histories;
    }

    /// <summary>
    /// 주문 실행 공통
    /// </summary>
    private async Task<OrderResult> PlaceOrderAsync(
        string symbol,
        string side,
        string positionSide,
        decimal quantity,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            { "symbol", symbol },
            { "side", side },
            { "positionSide", positionSide },
            { "type", "MARKET" },
            { "quantity", quantity.ToString("0.########") }
        };
        var queryString = _authenticator.CreateSignedQueryString(parameters);
        var request = CreateRequest(HttpMethod.Post, $"{SwapApiPath}/trade/order?{queryString}");

        _logger.LogInformation("BingX 주문 요청: {Symbol} {Side} {PositionSide} {Qty}",
            symbol, side, positionSide, quantity);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var result = await ParseResponseAsync<BingXOrderResponse>(response, cancellationToken);

        if (result?.Code != 0)
        {
            var errorMsg = result?.Msg ?? "Unknown error";
            _logger.LogError("BingX 주문 실패: {Error}", errorMsg);
            return OrderResult.Failure($"BingX 주문 실패: {errorMsg}");
        }

        if (result?.Data?.Order == null)
        {
            return OrderResult.Failure("주문 응답 파싱 실패");
        }

        var order = result.Data.Order;
        _logger.LogInformation("BingX 주문 성공: {OrderId}", order.OrderId);

        // 체결 대기 후 결과 조회
        await Task.Delay(500, cancellationToken);
        return await GetOrderAsync(order.OrderId.ToString(), cancellationToken);
    }

    /// <summary>
    /// 요청 생성 (API Key 헤더 포함)
    /// </summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-BX-APIKEY", _authenticator.ApiKey);
        return request;
    }

    /// <summary>
    /// 응답 파싱
    /// </summary>
    private async Task<T?> ParseResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("BingX API 오류: {StatusCode} - {Content}",
                response.StatusCode, content);
            throw new HttpRequestException($"BingX API 오류: {response.StatusCode} - {content}");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(content, _jsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "BingX 응답 파싱 실패: {Content}", content);
            throw;
        }
    }

    /// <summary>
    /// 응답 성공 확인
    /// </summary>
    private async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("BingX API 오류: {StatusCode} - {Content}",
                response.StatusCode, content);
            throw new HttpRequestException($"BingX API 오류: {response.StatusCode} - {content}");
        }

        var result = JsonSerializer.Deserialize<BingXBaseResponse>(content, _jsonOptions);
        if (result?.Code != 0)
        {
            _logger.LogError("BingX API 오류: {Code} - {Msg}", result?.Code, result?.Msg);
            throw new HttpRequestException($"BingX API 오류: {result?.Code} - {result?.Msg}");
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

#region Response Models

internal class BingXBaseResponse
{
    public int Code { get; set; }
    public string? Msg { get; set; }
}

internal class BingXBalanceResponse : BingXBaseResponse
{
    public BingXBalanceData? Data { get; set; }
}

internal class BingXBalanceData
{
    public BingXBalanceInfo? Balance { get; set; }
}

internal class BingXBalanceInfo
{
    public string Balance { get; set; } = "0";
    public string Equity { get; set; } = "0";
    public string AvailableMargin { get; set; } = "0";
    public string UsedMargin { get; set; } = "0";
    public string FreezeMargin { get; set; } = "0";

    public decimal GetAvailableMargin() => decimal.TryParse(AvailableMargin, out var v) ? v : 0;
}

internal class BingXPositionsResponse : BingXBaseResponse
{
    public List<BingXPositionInfo>? Data { get; set; }
}

internal class BingXPositionInfo
{
    public string Symbol { get; set; } = string.Empty;
    public string PositionSide { get; set; } = string.Empty;
    public decimal PositionAmt { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal UnrealizedProfit { get; set; }
    public int Leverage { get; set; }
    public decimal LiquidationPrice { get; set; }
}

internal class BingXOrderResponse : BingXBaseResponse
{
    public BingXOrderData? Data { get; set; }
}

internal class BingXOrderData
{
    public BingXOrderInfo? Order { get; set; }
}

internal class BingXOrderInfo
{
    public long OrderId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string PositionSide { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal OrigQty { get; set; }
    public decimal ExecutedQty { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal Commission { get; set; }
}

internal class BingXPriceResponse : BingXBaseResponse
{
    public BingXPriceData? Data { get; set; }
}

internal class BingXPriceData
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

internal class BingXAllOrdersResponse : BingXBaseResponse
{
    public BingXAllOrdersData? Data { get; set; }
}

internal class BingXAllOrdersData
{
    public List<BingXOrderHistoryInfo>? Orders { get; set; }
}

internal class BingXOrderHistoryInfo
{
    public long OrderId { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public string PositionSide { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal OrigQty { get; set; }
    public decimal ExecutedQty { get; set; }
    public decimal AvgPrice { get; set; }
    public decimal Commission { get; set; }
    public decimal Profit { get; set; }
    public long Time { get; set; }
    public long UpdateTime { get; set; }
}

#endregion
