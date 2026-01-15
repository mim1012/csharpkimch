using System.Text.Json.Serialization;

namespace KimchiHedge.AuthServer.Models;

/// <summary>
/// 김치프리미엄 데이터 (Webhook 수신용)
/// </summary>
public class KimchiPremiumData
{
    /// <summary>
    /// 김치프리미엄 (%)
    /// </summary>
    [JsonPropertyName("kimchi")]
    public decimal Kimchi { get; set; }

    /// <summary>
    /// 업비트 BTC/KRW 가격
    /// </summary>
    [JsonPropertyName("upbit")]
    public decimal Upbit { get; set; }

    /// <summary>
    /// 해외 BTC/USD 가격
    /// </summary>
    [JsonPropertyName("global")]
    public decimal Global { get; set; }

    /// <summary>
    /// Unix timestamp
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    /// <summary>
    /// 서버 수신 시각 (자동 설정)
    /// </summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
