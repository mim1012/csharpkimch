namespace KimchiHedge.Core.Auth.Models;

/// <summary>
/// 토큰 갱신 요청
/// </summary>
public class RefreshTokenRequest
{
    /// <summary>
    /// 리프레시 토큰
    /// </summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// 하드웨어 ID
    /// </summary>
    public string Hwid { get; set; } = string.Empty;
}

/// <summary>
/// 토큰 갱신 응답
/// </summary>
public class RefreshTokenResponse
{
    /// <summary>
    /// 새 액세스 토큰
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// 새 리프레시 토큰 (옵션)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// 토큰 타입
    /// </summary>
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// 만료 시간 (초)
    /// </summary>
    public int ExpiresIn { get; set; }
}
