namespace KimchiHedge.Core.Models;

/// <summary>
/// 김치프리미엄 데이터 모델
/// </summary>
public class KimchiPremiumData
{
    /// <summary>
    /// 김치프리미엄 (%)
    /// </summary>
    public decimal Kimchi { get; set; }

    /// <summary>
    /// 업비트 BTC/KRW 가격
    /// </summary>
    public decimal Upbit { get; set; }

    /// <summary>
    /// 해외 거래소 BTCUSDT 가격
    /// </summary>
    public decimal Global { get; set; }

    /// <summary>
    /// USD/KRW 환율
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Unix 타임스탬프
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    /// 데이터 수신 시간
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
