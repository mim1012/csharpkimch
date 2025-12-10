using KimchiHedge.Core.Auth.Enums;

namespace KimchiHedge.AuthServer.Entities;

/// <summary>
/// 사용자 엔티티
/// </summary>
public class User
{
    public Guid Id { get; set; }

    /// <summary>
    /// 사용자 고유 ID (USR-001 형식)
    /// </summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>
    /// 이메일
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 비밀번호 해시 (bcrypt)
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 라이선스 상태
    /// </summary>
    public LicenseStatus LicenseStatus { get; set; } = LicenseStatus.Pending;

    /// <summary>
    /// 라이선스 만료일
    /// </summary>
    public DateTime? LicenseExpiresAt { get; set; }

    /// <summary>
    /// 하드웨어 ID (SHA256)
    /// </summary>
    public string? Hwid { get; set; }

    /// <summary>
    /// HWID 등록일
    /// </summary>
    public DateTime? HwidRegisteredAt { get; set; }

    /// <summary>
    /// 관리자 여부
    /// </summary>
    public bool IsAdmin { get; set; }

    /// <summary>
    /// 생성일
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 수정일
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// 마지막 로그인
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// 세션 목록
    /// </summary>
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
