using KimchiHedge.Core.Models;

namespace KimchiHedge.Core.Exchanges;

/// <summary>
/// 현물 거래소 인터페이스
/// </summary>
public interface ISpotExchange
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
    /// 자산 잔고 조회
    /// </summary>
    /// <param name="asset">자산 (예: BTC, KRW)</param>
    Task<decimal> GetBalanceAsync(string asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// 시장가 매수 (Quote 기준)
    /// </summary>
    /// <param name="symbol">심볼 (예: BTC/KRW)</param>
    /// <param name="quoteAmount">매수 금액 (KRW)</param>
    Task<OrderResult> PlaceMarketBuyAsync(
        string symbol,
        decimal quoteAmount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 시장가 매도 (Base 기준)
    /// </summary>
    /// <param name="symbol">심볼 (예: BTC/KRW)</param>
    /// <param name="baseAmount">매도 수량 (BTC)</param>
    Task<OrderResult> PlaceMarketSellAsync(
        string symbol,
        decimal baseAmount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 전량 시장가 매도
    /// </summary>
    /// <param name="symbol">심볼</param>
    Task<OrderResult> PlaceMarketSellAllAsync(
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

    /// <summary>
    /// 체결 완료된 주문 내역 조회
    /// </summary>
    /// <param name="symbol">심볼 (null이면 전체)</param>
    /// <param name="startTime">시작 시간 (null이면 제한 없음)</param>
    /// <param name="endTime">종료 시간 (null이면 현재)</param>
    /// <param name="limit">조회 개수 (기본값: 100)</param>
    Task<List<TradeHistory>> GetOrderHistoryAsync(
        string? symbol = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
