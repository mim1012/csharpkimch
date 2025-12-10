namespace KimchiHedge.Core.Auth.Models;

/// <summary>
/// 로그인 요청
/// </summary>
public class LoginRequest
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
    /// 하드웨어 ID (자동 생성)
    /// </summary>
    public string Hwid { get; set; } = string.Empty;

    /// <summary>
    /// 클라이언트 버전
    /// </summary>
    public string? ClientVersion { get; set; }
}
