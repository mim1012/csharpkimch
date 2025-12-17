using System.Timers;
using KimchiHedge.Client.Services.Http;
using KimchiHedge.Core.Auth.Interfaces;
using KimchiHedge.Core.Auth.Models;
using KimchiHedge.Core.Security;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace KimchiHedge.Client.Services.Auth;

/// <summary>
/// 인증 서비스 - IAuthService 구현
/// </summary>
public class AuthService : IAuthService, IDisposable
{
    private readonly ApiClient _apiClient;
    private readonly ITokenStorage _tokenStorage;
    private readonly HwidGenerator _hwidGenerator;
    private readonly ILogger<AuthService> _logger;

    private Timer? _heartbeatTimer;
    private UserInfo? _currentUser;
    private bool _disposed;

    public event EventHandler<AuthStateChangedEventArgs>? AuthStateChanged;

    public bool IsAuthenticated => _tokenStorage.HasAccessToken && _currentUser != null;
    public UserInfo? CurrentUser => _currentUser;

    public AuthService(
        ApiClient apiClient,
        ITokenStorage tokenStorage,
        HwidGenerator hwidGenerator,
        ILogger<AuthService> logger)
    {
        _apiClient = apiClient;
        _tokenStorage = tokenStorage;
        _hwidGenerator = hwidGenerator;
        _logger = logger;
    }

    /// <summary>
    /// 회원가입
    /// </summary>
    public async Task<ApiResponse<RegisterResponse>> RegisterAsync(string email, string password, string? referralUid = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Attempting registration for {Email}", email);

        var request = new RegisterRequest
        {
            Email = email,
            Password = password,
            ReferralUid = referralUid
        };

        var response = await _apiClient.PostAsync<RegisterResponse>(ApiEndpoints.Auth.Register, request, ct);

        if (response.Success && response.Data != null)
        {
            _logger.LogInformation("Registration successful for {Email} with UID {Uid}", email, response.Data.Uid);
        }
        else
        {
            _logger.LogWarning("Registration failed for {Email}: {Error}", email, response.Error?.Message);
        }

        return response;
    }

    /// <summary>
    /// 로그인
    /// </summary>
    public async Task<ApiResponse<LoginResponse>> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        _logger.LogInformation("Attempting login for {Email}", email);

        // 비동기 HWID 생성 (UI 스레드 차단 방지)
        var hwid = await _hwidGenerator.GenerateHwidAsync();

        var request = new LoginRequest
        {
            Email = email,
            Password = password,
            Hwid = hwid
        };

        var response = await _apiClient.PostAsync<LoginResponse>(ApiEndpoints.Auth.Login, request, ct);

        if (response.Success && response.Data != null)
        {
            _tokenStorage.StoreTokens(response.Data.AccessToken, response.Data.RefreshToken, response.Data.ExpiresIn);
            _currentUser = response.Data.User;

            _logger.LogInformation("Login successful for {Email}", email);
            OnAuthStateChanged(true);
        }
        else
        {
            _logger.LogWarning("Login failed for {Email}: {Error}",
                email, response.Error?.Message);
        }

        return response;
    }

    /// <summary>
    /// 토큰 갱신
    /// </summary>
    public async Task<ApiResponse<RefreshTokenResponse>> RefreshTokenAsync(CancellationToken ct = default)
    {
        var refreshToken = _tokenStorage.GetRefreshToken();
        if (string.IsNullOrEmpty(refreshToken))
        {
            return ApiResponse<RefreshTokenResponse>.Fail("NO_TOKEN", "리프레시 토큰이 없습니다.");
        }

        _logger.LogDebug("Refreshing access token");

        var request = new RefreshTokenRequest { RefreshToken = refreshToken };
        var response = await _apiClient.PostAsync<RefreshTokenResponse>(ApiEndpoints.Auth.Refresh, request, ct);

        if (response.Success && response.Data != null)
        {
            // 새 RefreshToken이 없으면 기존 것 유지
            var newRefreshToken = response.Data.RefreshToken ?? refreshToken;
            _tokenStorage.StoreTokens(response.Data.AccessToken, newRefreshToken, response.Data.ExpiresIn);
            _logger.LogDebug("Token refreshed successfully");
        }
        else
        {
            _logger.LogWarning("Token refresh failed: {Error}", response.Error?.Message);

            // 갱신 실패시 로그아웃 처리
            await LogoutAsync();
        }

        return response;
    }

    /// <summary>
    /// 로그아웃
    /// </summary>
    public async Task LogoutAsync()
    {
        _logger.LogInformation("Logging out");

        StopHeartbeat();

        // 서버에 로그아웃 요청 (실패해도 무시)
        if (_tokenStorage.HasAccessToken)
        {
            try
            {
                await _apiClient.PostAsync<object>(ApiEndpoints.Auth.Logout, null);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Server logout request failed");
            }
        }

        _tokenStorage.ClearTokens();
        _currentUser = null;

        OnAuthStateChanged(false);
    }

    /// <summary>
    /// 라이선스 상태 조회
    /// </summary>
    public async Task<ApiResponse<LicenseStatusResponse>> GetLicenseStatusAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            return ApiResponse<LicenseStatusResponse>.Fail("NOT_AUTHENTICATED", "로그인이 필요합니다.");
        }

        return await _apiClient.GetAsync<LicenseStatusResponse>(ApiEndpoints.License.Status, ct);
    }

    /// <summary>
    /// 하트비트 전송
    /// </summary>
    public async Task<ApiResponse<HeartbeatResponse>> SendHeartbeatAsync(CancellationToken ct = default)
    {
        if (!IsAuthenticated)
        {
            return ApiResponse<HeartbeatResponse>.Fail("NOT_AUTHENTICATED", "로그인이 필요합니다.");
        }

        // 캐시된 HWID 사용 (이미 로그인 시 생성됨)
        var hwid = await _hwidGenerator.GenerateHwidAsync();
        var request = new HeartbeatRequest
        {
            Hwid = hwid,
            Timestamp = DateTime.UtcNow
        };

        var response = await _apiClient.PostAsync<HeartbeatResponse>(ApiEndpoints.License.Heartbeat, request, ct);

        if (!response.Success)
        {
            _logger.LogWarning("Heartbeat failed: {Error}", response.Error?.Message);

            // 라이선스 문제로 하트비트 실패시 로그아웃
            if (response.Error?.Code?.StartsWith("LIC_") == true ||
                response.Error?.Code?.StartsWith("HWID_") == true)
            {
                await LogoutAsync();
            }
        }
        else if (response.Data?.ForceLogout == true)
        {
            _logger.LogWarning("Force logout requested by server");
            await LogoutAsync();
        }

        return response;
    }

    /// <summary>
    /// 하트비트 타이머 시작
    /// </summary>
    public void StartHeartbeat()
    {
        if (_heartbeatTimer != null)
            return;

        _heartbeatTimer = new Timer(5 * 60 * 1000); // 5분
        _heartbeatTimer.Elapsed += OnHeartbeatTimer;
        _heartbeatTimer.AutoReset = true;
        _heartbeatTimer.Start();

        _logger.LogInformation("Heartbeat timer started");
    }

    /// <summary>
    /// 하트비트 타이머 중지
    /// </summary>
    public void StopHeartbeat()
    {
        if (_heartbeatTimer == null)
            return;

        _heartbeatTimer.Stop();
        _heartbeatTimer.Elapsed -= OnHeartbeatTimer;
        _heartbeatTimer.Dispose();
        _heartbeatTimer = null;

        _logger.LogInformation("Heartbeat timer stopped");
    }

    private async void OnHeartbeatTimer(object? sender, ElapsedEventArgs e)
    {
        try
        {
            await SendHeartbeatAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Heartbeat timer error");
        }
    }

    private void OnAuthStateChanged(bool isAuthenticated)
    {
        AuthStateChanged?.Invoke(this, new AuthStateChangedEventArgs(isAuthenticated, _currentUser));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        StopHeartbeat();
        _disposed = true;
    }
}
