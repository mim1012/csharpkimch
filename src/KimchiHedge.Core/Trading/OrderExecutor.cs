using KimchiHedge.Core.Exchanges;
using KimchiHedge.Core.Models;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 주문 실행 결과
/// </summary>
public class ExecutionResult
{
    public bool Success { get; set; }
    public decimal UpbitBtcAmount { get; set; }
    public decimal UpbitEntryPrice { get; set; }
    public decimal UpbitFee { get; set; }
    public decimal FuturesShortAmount { get; set; }
    public decimal FuturesEntryPrice { get; set; }
    public decimal FuturesFee { get; set; }
    public string? ErrorMessage { get; set; }
    public bool NeedsRollback { get; set; }

    /// <summary>
    /// 수량 일치 여부 (100% 동일해야 함)
    /// </summary>
    public bool IsSynchronized => UpbitBtcAmount == FuturesShortAmount;

    public static ExecutionResult Failure(string error, bool needsRollback = false)
        => new() { Success = false, ErrorMessage = error, NeedsRollback = needsRollback };
}

/// <summary>
/// 주문 실행 (단일 책임: 주문 실행만)
/// 업비트 현물 매수 → BingX 선물 숏 (1:1 헷지)
/// </summary>
public class OrderExecutor
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
    /// 헷지 진입 실행 (업비트 매수 → BingX 숏)
    /// </summary>
    /// <param name="entryRatio">시드 대비 진입 비율 (%)</param>
    /// <param name="leverage">레버리지 배율</param>
    public async Task<ExecutionResult> ExecuteEntryAsync(decimal entryRatio, int leverage)
    {
        var result = new ExecutionResult();

        try
        {
            // 1. KRW 잔고 확인 및 매수 금액 계산
            var krwBalance = await _spotExchange.GetBalanceAsync("KRW");
            var buyAmount = krwBalance * (entryRatio / 100);

            _logger.LogInformation("업비트 매수 시작. 잔고: {Balance} KRW, 매수금액: {Amount} KRW",
                krwBalance, buyAmount);

            // 2. 업비트 시장가 매수
            var buyResult = await _spotExchange.PlaceMarketBuyAsync("BTC/KRW", buyAmount);

            if (!buyResult.IsSuccess)
            {
                _logger.LogError("업비트 매수 실패: {Error}", buyResult.ErrorMessage);
                return ExecutionResult.Failure($"업비트 매수 실패: {buyResult.ErrorMessage}");
            }

            result.UpbitBtcAmount = buyResult.ExecutedQuantity;
            result.UpbitEntryPrice = buyResult.AveragePrice;
            result.UpbitFee = buyResult.Fee;

            _logger.LogInformation("업비트 매수 완료. 체결수량: {Qty} BTC, 체결가: {Price}",
                buyResult.ExecutedQuantity, buyResult.AveragePrice);

            // 3. 레버리지 설정
            await _futuresExchange.SetLeverageAsync("BTCUSDT", leverage);

            // 4. BingX 시장가 숏 (업비트 체결 수량과 100% 동일)
            var targetShortAmount = result.UpbitBtcAmount;
            _logger.LogInformation("BingX 숏 진입 시작. 목표수량: {Qty} BTC", targetShortAmount);

            var shortResult = await _futuresExchange.OpenShortAsync("BTCUSDT", targetShortAmount);

            if (!shortResult.IsSuccess)
            {
                _logger.LogError("BingX 숏 실패: {Error}", shortResult.ErrorMessage);
                return ExecutionResult.Failure($"BingX 숏 실패: {shortResult.ErrorMessage}", needsRollback: true);
            }

            result.FuturesShortAmount = shortResult.ExecutedQuantity;
            result.FuturesEntryPrice = shortResult.AveragePrice;
            result.FuturesFee = shortResult.Fee;

            _logger.LogInformation("BingX 숏 완료. 체결수량: {Qty} BTC, 체결가: {Price}",
                shortResult.ExecutedQuantity, shortResult.AveragePrice);

            // 5. 수량 일치 확인 (허용 오차 0)
            if (!result.IsSynchronized)
            {
                _logger.LogError("수량 불일치! 업비트: {Upbit} BTC, BingX: {BingX} BTC",
                    result.UpbitBtcAmount, result.FuturesShortAmount);
                return ExecutionResult.Failure("수량 불일치 발생", needsRollback: true);
            }

            result.Success = true;
            _logger.LogInformation("헷지 진입 성공! 1:1 동기화 완료");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "주문 실행 중 예외 발생");
            return ExecutionResult.Failure(ex.Message, needsRollback: result.UpbitBtcAmount > 0);
        }
    }

    /// <summary>
    /// 헷지 청산 실행 (업비트 매도 + BingX 숏 청산)
    /// </summary>
    public async Task<ExecutionResult> ExecuteCloseAsync()
    {
        var result = new ExecutionResult();

        try
        {
            _logger.LogInformation("포지션 청산 시작");

            // 1. 업비트 전량 시장가 매도
            var sellResult = await _spotExchange.PlaceMarketSellAllAsync("BTC/KRW");
            _logger.LogInformation("업비트 매도 완료");

            // 2. BingX 숏 포지션 청산
            var closeResult = await _futuresExchange.ClosePositionAsync("BTCUSDT");
            _logger.LogInformation("BingX 청산 완료");

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "청산 실행 중 예외 발생");
            return ExecutionResult.Failure(ex.Message);
        }
    }
}
