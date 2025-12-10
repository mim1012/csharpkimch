using KimchiHedge.Core.Auth.Enums;

namespace KimchiHedge.Core.Auth.Models;

/// <summary>
/// 로그인 응답
/// </summary>
public class LoginResponse
{
    /// <summary>
    /// 액세스 토큰 (JWT)
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 리프레시 토큰
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// 토큰 타입 (Bearer)
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// 액세스 토큰 만료 시간 (초)
    /// </summary>
    public int ExpiresIn { get; set; }

    /// <summary>
    /// 사용자 정보
    /// </summary>
    public UserInfo User { get; set; } = new();
}

/// <summary>
/// 사용자 정보
/// </summary>
public class UserInfo
{
    /// <summary>
    /// 사용자 고유 ID (USR-001 형식)
    /// </summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>
    /// 이메일
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 라이선스 상태
    /// </summary>
    public LicenseStatus LicenseStatus { get; set; }

    /// <summary>
    /// 라이선스 만료일
    /// </summary>
    public DateTime? LicenseExpiresAt { get; set; }

    /// <summary>
    /// 관리자 여부
    /// </summary>
    public bool IsAdmin { get; set; }
}
