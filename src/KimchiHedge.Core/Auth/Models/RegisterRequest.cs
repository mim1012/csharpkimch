namespace KimchiHedge.Core.Auth.Models;

/// <summary>
/// 회원가입 요청 DTO
/// </summary>
public class RegisterRequest
{
    /// <summary>
    /// 이메일
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 비밀번호
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// 추천인 UID (선택사항, 예: USR-001)
    /// </summary>
    public string? ReferralUid { get; set; }
}
