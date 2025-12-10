using KimchiHedge.Core.Auth.Enums;

namespace KimchiHedge.Core.Auth.Models;

/// <summary>
/// 하트비트 요청
/// </summary>
public class HeartbeatRequest
{
    /// <summary>
    /// 하드웨어 ID
    /// </summary>
    public string Hwid { get; set; } = string.Empty;

    /// <summary>
    /// 클라이언트 버전
    /// </summary>
    public string? ClientVersion { get; set; }

    /// <summary>
    /// 클라이언트 타임스탬프
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// 하트비트 응답
/// </summary>
public class HeartbeatResponse
{
    /// <summary>
    /// 서버 시간
    /// </summary>
    public DateTime ServerTime { get; set; }

    /// <summary>
    /// 라이선스 유효 여부
    /// </summary>
    public bool LicenseValid { get; set; }

    /// <summary>
    /// 라이선스 상태
    /// </summary>
    public LicenseStatus LicenseStatus { get; set; }

    /// <summary>
    /// 라이선스 만료일
    /// </summary>
    public DateTime? LicenseExpiresAt { get; set; }

    /// <summary>
    /// 다음 하트비트 간격 (초)
    /// </summary>
    public int NextHeartbeatSeconds { get; set; } = 300; // 5분

    /// <summary>
    /// 서버 메시지 (공지 등)
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// 강제 로그아웃 필요 여부
    /// </summary>
    public bool ForceLogout { get; set; }
}
