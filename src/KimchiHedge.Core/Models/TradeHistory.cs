namespace KimchiHedge.Core.Models;

/// <summary>
/// 거래 내역 항목
/// </summary>
public class TradeHistory
{
    /// <summary>
    /// 주문 ID
    /// </summary>
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// 심볼 (예: BTC/KRW, BTC-USDT)
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// 주문 방향 (Buy/Sell)
    /// </summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>
    /// 주문 타입 (Market/Limit)
    /// </summary>
    public string OrderType { get; set; } = string.Empty;

    /// <summary>
    /// 체결 수량
    /// </summary>
    public decimal ExecutedQuantity { get; set; }

    /// <summary>
    /// 평균 체결가
    /// </summary>
    public decimal AveragePrice { get; set; }

    /// <summary>
    /// 체결 금액 (Quantity * Price)
    /// </summary>
    public decimal ExecutedAmount { get; set; }

    /// <summary>
    /// 수수료
    /// </summary>
    public decimal Fee { get; set; }

    /// <summary>
    /// 주문 상태 (done, cancel 등)
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// 주문 생성 시간
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 거래소 이름
    /// </summary>
    public string Exchange { get; set; } = string.Empty;

    /// <summary>
    /// 실현 손익 (선물용)
    /// </summary>
    public decimal? RealizedPnL { get; set; }
}
