using KimchiHedge.Core.Exchanges;
using KimchiHedge.Core.Models;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 주문 실행 (단일 책임: 주문 실행만)
/// 업비트 현물 매수 → BingX 선물 숏 (1:1 헷지)
/// </summary>
public class OrderExecutor : IOrderExecutor
{
    private readonly ISpotExchange _spotExchange;
    private readonly IFuturesExchange _futuresExchange;
    private readonly ILogger<OrderExecutor> _logger;

    public OrderExecutor(
        ISpotExchange spotExchange,
        IFuturesExchange futuresExchange,
        ILogger<OrderExecutor> logger)
    {
        _spotExchange = spotExchange;
        _futuresExchange = futuresExchange;
        _logger = logger;
    }

    /// <summary>
    /// 업비트 시장가 매수
    /// </summary>
    public async Task<OrderResult> ExecuteUpbitBuyAsync(decimal entryRatio)
    {
        try
        {
            var krwBalance = await _spotExchange.GetBalanceAsync("KRW");
            var buyAmount = krwBalance * (entryRatio / 100);

            _logger.LogInformation("업비트 매수 시작. 잔고: {Balance} KRW, 매수금액: {Amount} KRW",
                krwBalance, buyAmount);

            var buyResult = await _spotExchange.PlaceMarketBuyAsync("BTC/KRW", buyAmount);

            if (!buyResult.IsSuccess)
            {
                _logger.LogError("업비트 매수 실패: {Error}", buyResult.ErrorMessage);
                return OrderResult.Failure($"업비트 매수 실패: {buyResult.ErrorMessage}");
            }

            _logger.LogInformation("업비트 매수 완료. 체결수량: {Qty} BTC, 체결가: {Price}",
                buyResult.ExecutedQuantity, buyResult.AveragePrice);

            return OrderResult.Success(buyResult.ExecutedQuantity, buyResult.AveragePrice, buyResult.Fee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업비트 매수 중 예외 발생");
            return OrderResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// 레버리지 설정
    /// </summary>
    public async Task SetLeverageAsync(int leverage)
    {
        await _futuresExchange.SetLeverageAsync("BTCUSDT", leverage);
        _logger.LogInformation("레버리지 설정 완료: {Leverage}배", leverage);
    }

    /// <summary>
    /// BingX 시장가 숏 진입
    /// </summary>
    public async Task<OrderResult> ExecuteBingXShortAsync(decimal quantity)
    {
        try
        {
            _logger.LogInformation("BingX 숏 진입 시작. 수량: {Qty} BTC", quantity);

            var shortResult = await _futuresExchange.OpenShortAsync("BTCUSDT", quantity);

            if (!shortResult.IsSuccess)
            {
                _logger.LogError("BingX 숏 실패: {Error}", shortResult.ErrorMessage);
                return OrderResult.Failure($"BingX 숏 실패: {shortResult.ErrorMessage}");
            }

            _logger.LogInformation("BingX 숏 완료. 체결수량: {Qty} BTC, 체결가: {Price}",
                shortResult.ExecutedQuantity, shortResult.AveragePrice);

            return OrderResult.Success(shortResult.ExecutedQuantity, shortResult.AveragePrice, shortResult.Fee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BingX 숏 중 예외 발생");
            return OrderResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// 업비트 전량 시장가 매도
    /// </summary>
    public async Task<OrderResult> ExecuteUpbitSellAllAsync()
    {
        try
        {
            _logger.LogInformation("업비트 전량 매도 시작");

            var sellResult = await _spotExchange.PlaceMarketSellAllAsync("BTC/KRW");

            if (!sellResult.IsSuccess)
            {
                _logger.LogError("업비트 매도 실패: {Error}", sellResult.ErrorMessage);
                return OrderResult.Failure($"업비트 매도 실패: {sellResult.ErrorMessage}");
            }

            _logger.LogInformation("업비트 매도 완료. 체결수량: {Qty} BTC, 체결가: {Price}",
                sellResult.ExecutedQuantity, sellResult.AveragePrice);

            return OrderResult.Success(sellResult.ExecutedQuantity, sellResult.AveragePrice, sellResult.Fee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "업비트 매도 중 예외 발생");
            return OrderResult.Failure(ex.Message);
        }
    }

    /// <summary>
    /// BingX 숏 포지션 전량 청산
    /// </summary>
    public async Task<OrderResult> ExecuteBingXCloseAsync()
    {
        try
        {
            _logger.LogInformation("BingX 숏 청산 시작");

            var closeResult = await _futuresExchange.ClosePositionAsync("BTCUSDT");

            if (!closeResult.IsSuccess)
            {
                _logger.LogError("BingX 청산 실패: {Error}", closeResult.ErrorMessage);
                return OrderResult.Failure($"BingX 청산 실패: {closeResult.ErrorMessage}");
            }

            _logger.LogInformation("BingX 청산 완료. 체결수량: {Qty} BTC, 체결가: {Price}",
                closeResult.ExecutedQuantity, closeResult.AveragePrice);

            return OrderResult.Success(closeResult.ExecutedQuantity, closeResult.AveragePrice, closeResult.Fee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BingX 청산 중 예외 발생");
            return OrderResult.Failure(ex.Message);
        }
    }
}
