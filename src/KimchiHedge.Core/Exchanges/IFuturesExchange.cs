using KimchiHedge.Core.Models;

namespace KimchiHedge.Core.Exchanges;

/// <summary>
/// 선물 포지션 정보
/// </summary>
public class FuturesPosition
{
    /// <summary>
    /// 심볼
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 포지션 방향 (Long/Short)
    /// </summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>
    /// 포지션 수량
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// 진입가
    /// </summary>
    public decimal EntryPrice { get; set; }

    /// <summary>
    /// 미실현 손익
    /// </summary>
    public decimal UnrealizedPnL { get; set; }

    /// <summary>
    /// 레버리지
    /// </summary>
    public int Leverage { get; set; }

    /// <summary>
    /// 청산가
    /// </summary>
    public decimal LiquidationPrice { get; set; }
}

/// <summary>
/// 선물 거래소 인터페이스
/// </summary>
public interface IFuturesExchange
{
    /// <summary>
    /// 거래소 이름
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 연결 여부
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// 거래소 연결
    /// </summary>
    Task ConnectAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 거래소 연결 해제
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// USDT 잔고 조회
    /// </summary>
    Task<decimal> GetBalanceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 현재 포지션 조회
    /// </summary>
    /// <param name="symbol">심볼 (예: BTCUSDT)</param>
    Task<FuturesPosition?> GetPositionAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 레버리지 설정
    /// </summary>
    /// <param name="symbol">심볼</param>
    /// <param name="leverage">레버리지 배율</param>
    Task SetLeverageAsync(
        string symbol,
        int leverage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 숏 포지션 오픈 (시장가)
    /// </summary>
    /// <param name="symbol">심볼</param>
    /// <param name="quantity">수량</param>
    Task<OrderResult> OpenShortAsync(
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 롱 포지션 오픈 (시장가)
    /// </summary>
    /// <param name="symbol">심볼</param>
    /// <param name="quantity">수량</param>
    Task<OrderResult> OpenLongAsync(
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 포지션 전량 청산 (시장가)
    /// </summary>
    /// <param name="symbol">심볼</param>
    Task<OrderResult> ClosePositionAsync(
        string symbol,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 주문 조회
    /// </summary>
    /// <param name="orderId">주문 ID</param>
    Task<OrderResult> GetOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 현재가 조회
    /// </summary>
    /// <param name="symbol">심볼</param>
    Task<decimal> GetCurrentPriceAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}
