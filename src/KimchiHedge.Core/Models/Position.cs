namespace KimchiHedge.Core.Models;

/// <summary>
/// 포지션 상태
/// </summary>
public enum PositionStatus
{
    None,           // 포지션 없음
    Opening,        // 진입 중
    Open,           // 포지션 보유 중
    Closing,        // 청산 중
    Closed,         // 청산 완료
    Rollback        // 롤백 중
}

/// <summary>
/// 포지션 종료 이유
/// </summary>
public enum CloseReason
{
    TakeProfit,     // 익절
    StopLoss,       // 손절
    Manual,         // 수동 청산
    Rollback,       // 롤백
    QuantityMismatch, // 수량 불일치로 인한 롤백
    Error           // 오류로 인한 청산
}

/// <summary>
/// 헷지 포지션 정보 (업비트 현물 + BingX 선물 1:1 쌍)
/// </summary>
public class Position
{
    /// <summary>
    /// 포지션 ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 포지션 상태
    /// </summary>
    public PositionStatus Status { get; set; } = PositionStatus.None;

    /// <summary>
    /// 업비트 BTC 보유량
    /// </summary>
    public decimal UpbitBtcAmount { get; set; }

    /// <summary>
    /// 업비트 평균 진입가
    /// </summary>
    public decimal UpbitEntryPrice { get; set; }

    /// <summary>
    /// 해외 거래소 숏 수량 (업비트 체결량과 100% 동일해야 함)
    /// </summary>
    public decimal FuturesShortAmount { get; set; }

    /// <summary>
    /// 해외 거래소 평균 진입가
    /// </summary>
    public decimal FuturesEntryPrice { get; set; }

    /// <summary>
    /// 진입 시 김치프리미엄
    /// </summary>
    public decimal EntryKimchi { get; set; }

    /// <summary>
    /// 진입 시간
    /// </summary>
    public DateTime EntryTime { get; set; }

    /// <summary>
    /// 청산 시간
    /// </summary>
    public DateTime? CloseTime { get; set; }

    /// <summary>
    /// 청산 시 김치프리미엄
    /// </summary>
    public decimal? CloseKimchi { get; set; }

    /// <summary>
    /// 청산 이유
    /// </summary>
    public CloseReason? CloseReason { get; set; }

    /// <summary>
    /// 실현 손익 (KRW)
    /// </summary>
    public decimal? RealizedPnL { get; set; }

    /// <summary>
    /// 업비트 수수료
    /// </summary>
    public decimal UpbitFee { get; set; }

    /// <summary>
    /// 해외 거래소 수수료
    /// </summary>
    public decimal FuturesFee { get; set; }

    /// <summary>
    /// 수량 동기화 여부 (100% 정확히 일치해야 함, 허용 오차 0)
    /// </summary>
    public bool IsSynchronized => UpbitBtcAmount == FuturesShortAmount;

    /// <summary>
    /// 수량 차이 (불일치 시 즉시 롤백 필요)
    /// </summary>
    public decimal QuantityDifference => Math.Abs(UpbitBtcAmount - FuturesShortAmount);
}
