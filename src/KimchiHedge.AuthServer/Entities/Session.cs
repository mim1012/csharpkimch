namespace KimchiHedge.AuthServer.Entities;

/// <summary>
/// 세션 엔티티
/// </summary>
public class Session
{
    public Guid Id { get; set; }

    /// <summary>
    /// 사용자 ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 리프레시 토큰
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// 하드웨어 ID
    /// </summary>
    public string Hwid { get; set; } = string.Empty;

    /// <summary>
    /// 접속 IP
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User-Agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// 생성일
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 만료일
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// 활성 여부
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 사용자 (Navigation)
    /// </summary>
    public User User { get; set; } = null!;
}
