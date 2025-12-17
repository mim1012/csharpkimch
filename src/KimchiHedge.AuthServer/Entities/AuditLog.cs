namespace KimchiHedge.AuthServer.Entities;

/// <summary>
/// 감사 로그 엔티티
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    /// <summary>
    /// 사용자 ID (nullable - 로그인 실패 등)
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// 액션 (LOGIN, LOGOUT, LICENSE_CHECK, etc.)
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// 결과 (SUCCESS, FAILED)
    /// </summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>
    /// 접속 IP
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// 하드웨어 ID
    /// </summary>
    public string? Hwid { get; set; }

    /// <summary>
    /// 추가 정보 (JSON)
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// 생성일
    /// </summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 감사 로그 액션 타입
/// </summary>
public static class AuditAction
{
    public const string Register = "REGISTER";
    public const string Login = "LOGIN";
    public const string LoginFailed = "LOGIN_FAILED";
    public const string Logout = "LOGOUT";
    public const string TokenRefresh = "TOKEN_REFRESH";
    public const string Heartbeat = "HEARTBEAT";
    public const string LicenseCheck = "LICENSE_CHECK";
    public const string HwidRegistered = "HWID_REGISTERED";
    public const string HwidMismatch = "HWID_MISMATCH";
    public const string HwidReset = "HWID_RESET";
    public const string UserApproved = "USER_APPROVED";
    public const string UserSuspended = "USER_SUSPENDED";
    public const string LicenseExtended = "LICENSE_EXTENDED";
}

/// <summary>
/// 감사 로그 결과
/// </summary>
public static class AuditResult
{
    public const string Success = "SUCCESS";
    public const string Failed = "FAILED";
}
