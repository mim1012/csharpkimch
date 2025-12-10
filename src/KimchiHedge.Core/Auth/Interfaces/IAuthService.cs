using KimchiHedge.Core.Auth.Models;

namespace KimchiHedge.Core.Auth.Interfaces;

/// <summary>
/// 인증 상태 변경 이벤트 인자
/// </summary>
public class AuthStateChangedEventArgs : EventArgs
{
    public bool IsAuthenticated { get; }
    public UserInfo? User { get; }

    public AuthStateChangedEventArgs(bool isAuthenticated, UserInfo? user = null)
    {
        IsAuthenticated = isAuthenticated;
        User = user;
    }
}

/// <summary>
/// 인증 서비스 인터페이스
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 인증 상태 변경 이벤트
    /// </summary>
    event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

    /// <summary>
    /// 인증 여부
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 현재 사용자 정보
    /// </summary>
    UserInfo? CurrentUser { get; }

    /// <summary>
    /// 로그인
    /// </summary>
    Task<ApiResponse<LoginResponse>> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// 토큰 갱신
    /// </summary>
    Task<ApiResponse<RefreshTokenResponse>> RefreshTokenAsync(CancellationToken ct = default);

    /// <summary>
    /// 로그아웃
    /// </summary>
    Task LogoutAsync();

    /// <summary>
    /// 라이선스 상태 조회
    /// </summary>
    Task<ApiResponse<LicenseStatusResponse>> GetLicenseStatusAsync(CancellationToken ct = default);

    /// <summary>
    /// 하트비트 전송
    /// </summary>
    Task<ApiResponse<HeartbeatResponse>> SendHeartbeatAsync(CancellationToken ct = default);

    /// <summary>
    /// 하트비트 시작
    /// </summary>
    void StartHeartbeat();

    /// <summary>
    /// 하트비트 중지
    /// </summary>
    void StopHeartbeat();
}
