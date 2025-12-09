using KimchiHedge.Core.Exchanges;
using KimchiHedge.Core.Models;
using Microsoft.Extensions.Logging;

namespace KimchiHedge.Core.Trading;

/// <summary>
/// 롤백 서비스 (단일 책임: 롤백 처리만)
/// 수량 불일치 또는 오류 발생 시 즉시 롤백 실행
/// </summary>
public class RollbackService
{
    private readonly ISpotExchange _spotExchange;
    private readonly IFuturesExchange _futuresExchange;
    private readonly ILogger<RollbackService> _logger;

    public event EventHandler<string>? RollbackCompleted;
    public event EventHandler<string>? RollbackFailed;

    public RollbackService(
        ISpotExchange spotExchange,
        IFuturesExchange futuresExchange,
        ILogger<RollbackService> logger)
    {
        _spotExchange = spotExchange;
        _futuresExchange = futuresExchange;
        _logger = logger;
    }

    /// <summary>
    /// 롤백 실행
    /// 업비트 BTC 전량 매도 + BingX 포지션 청산
    /// </summary>
    /// <param name="reason">롤백 사유</param>
    public async Task<bool> ExecuteRollbackAsync(CloseReason reason)
    {
        _logger.LogWarning("롤백 시작 - 사유: {Reason}", reason);

        bool upbitRollbackSuccess = false;
        bool bingxRollbackSuccess = false;

        try
        {
            // 1. 업비트 BTC 전량 매도
            try
            {
                var btcBalance = await _spotExchange.GetBalanceAsync("BTC");
                if (btcBalance > 0)
                {
                    _logger.LogInformation("업비트 롤백 매도 실행. 잔고: {Balance} BTC", btcBalance);
                    await _spotExchange.PlaceMarketSellAllAsync("BTC/KRW");
                    _logger.LogInformation("업비트 롤백 매도 완료");
                }
                upbitRollbackSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "업비트 롤백 매도 실패");
            }

            // 2. BingX 포지션 청산
            try
            {
                var position = await _futuresExchange.GetPositionAsync("BTCUSDT");
                if (position != null && position.Quantity > 0)
                {
                    _logger.LogInformation("BingX 롤백 청산 실행. 포지션: {Qty} BTC", position.Quantity);
                    await _futuresExchange.ClosePositionAsync("BTCUSDT");
                    _logger.LogInformation("BingX 롤백 청산 완료");
                }
                bingxRollbackSuccess = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BingX 롤백 청산 실패");
            }

            // 결과 처리
            if (upbitRollbackSuccess && bingxRollbackSuccess)
            {
                _logger.LogInformation("롤백 완료");
                RollbackCompleted?.Invoke(this, reason.ToString());
                return true;
            }
            else
            {
                var failedParts = new List<string>();
                if (!upbitRollbackSuccess) failedParts.Add("업비트");
                if (!bingxRollbackSuccess) failedParts.Add("BingX");

                var errorMessage = $"롤백 부분 실패: {string.Join(", ", failedParts)}";
                _logger.LogError(errorMessage);
                RollbackFailed?.Invoke(this, errorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "롤백 중 예외 발생");
            RollbackFailed?.Invoke(this, ex.Message);
            return false;
        }
    }
}
