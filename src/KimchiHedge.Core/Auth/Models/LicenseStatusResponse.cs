using KimchiHedge.Core.Auth.Enums;

namespace KimchiHedge.Core.Auth.Models;

/// <summary>
/// 라이선스 상태 응답
/// </summary>
public class LicenseStatusResponse
{
    /// <summary>
    /// 사용자 UID
    /// </summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>
    /// 라이선스 상태
    /// </summary>
    public LicenseStatus Status { get; set; }

    /// <summary>
    /// 만료일
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// 남은 일수
    /// </summary>
    public int? DaysRemaining { get; set; }

    /// <summary>
    /// HWID 등록 여부
    /// </summary>
    public bool IsHwidRegistered { get; set; }

    /// <summary>
    /// 유효한 라이선스인지
    /// </summary>
    public bool IsValid { get; set; }
}
