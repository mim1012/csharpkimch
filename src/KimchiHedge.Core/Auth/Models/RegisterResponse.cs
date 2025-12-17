namespace KimchiHedge.Core.Auth.Models;

/// <summary>
/// 회원가입 응답 DTO
/// </summary>
public class RegisterResponse
{
    /// <summary>
    /// 생성된 사용자 UID (예: USR-001)
    /// </summary>
    public string Uid { get; set; } = string.Empty;

    /// <summary>
    /// 이메일
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// 안내 메시지
    /// </summary>
    public string Message { get; set; } = "가입 신청이 완료되었습니다. 관리자 승인 후 이용 가능합니다.";
}
