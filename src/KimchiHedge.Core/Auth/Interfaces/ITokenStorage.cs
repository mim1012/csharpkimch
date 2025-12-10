namespace KimchiHedge.Core.Auth.Interfaces;

/// <summary>
/// 토큰 저장소 인터페이스
/// </summary>
public interface ITokenStorage
{
    /// <summary>
    /// 액세스 토큰 보유 여부
    /// </summary>
    bool HasAccessToken { get; }

    /// <summary>
    /// 리프레시 토큰 보유 여부
    /// </summary>
    bool HasRefreshToken { get; }

    /// <summary>
    /// 액세스 토큰 만료 여부
    /// </summary>
    bool IsAccessTokenExpired { get; }

    /// <summary>
    /// 토큰 저장
    /// </summary>
    void StoreTokens(string accessToken, string refreshToken, int expiresInSeconds);

    /// <summary>
    /// 액세스 토큰 가져오기
    /// </summary>
    string? GetAccessToken();

    /// <summary>
    /// 리프레시 토큰 가져오기
    /// </summary>
    string? GetRefreshToken();

    /// <summary>
    /// 액세스 토큰만 업데이트
    /// </summary>
    void UpdateAccessToken(string accessToken, int expiresInSeconds);

    /// <summary>
    /// 토큰 삭제
    /// </summary>
    void ClearTokens();
}
