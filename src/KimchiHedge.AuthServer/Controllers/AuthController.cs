using KimchiHedge.AuthServer.Entities;
using KimchiHedge.AuthServer.Services;
using KimchiHedge.Core.Auth.Enums;
using KimchiHedge.Core.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimchiHedge.AuthServer.Controllers;

/// <summary>
/// 인증 API 컨트롤러
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly UserService _userService;
    private readonly SessionService _sessionService;
    private readonly JwtService _jwtService;
    private readonly AuditService _auditService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserService userService,
        SessionService sessionService,
        JwtService jwtService,
        AuditService auditService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userService = userService;
        _sessionService = sessionService;
        _jwtService = jwtService;
        _auditService = auditService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 로그인
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var ipAddress = GetClientIpAddress();

        // 1. 사용자 조회
        var user = await _userService.GetByEmailAsync(request.Email);
        if (user == null)
        {
            await _auditService.LogLoginFailedAsync(request.Email, ipAddress, request.Hwid, "User not found");
            return Unauthorized(ApiResponse<LoginResponse>.Fail(AuthErrorCode.AUTH_001_INVALID_CREDENTIALS));
        }

        // 2. 비밀번호 검증
        if (!_userService.VerifyPassword(user, request.Password))
        {
            await _auditService.LogLoginFailedAsync(request.Email, ipAddress, request.Hwid, "Invalid password");
            return Unauthorized(ApiResponse<LoginResponse>.Fail(AuthErrorCode.AUTH_001_INVALID_CREDENTIALS));
        }

        // 3. 라이선스 상태 검증
        var licenseValidation = _userService.ValidateLicense(user);
        if (!licenseValidation.IsValid)
        {
            var errorCode = user.LicenseStatus switch
            {
                LicenseStatus.Pending => AuthErrorCode.LIC_003_APPROVAL_PENDING,
                LicenseStatus.Suspended => AuthErrorCode.LIC_002_LICENSE_SUSPENDED,
                LicenseStatus.Expired => AuthErrorCode.LIC_001_LICENSE_EXPIRED,
                _ => AuthErrorCode.AUTH_001_INVALID_CREDENTIALS
            };

            await _auditService.LogLoginFailedAsync(request.Email, ipAddress, request.Hwid,
                licenseValidation.ErrorMessage ?? "License invalid");
            return Unauthorized(ApiResponse<LoginResponse>.Fail(errorCode));
        }

        // 4. HWID 검증
        var hwidValidation = _userService.ValidateHwid(user, request.Hwid);
        if (!hwidValidation.IsValid)
        {
            await _auditService.LogHwidMismatchAsync(user.Id, ipAddress, request.Hwid, user.Hwid);
            return Unauthorized(ApiResponse<LoginResponse>.Fail(AuthErrorCode.HWID_001_MISMATCH));
        }

        // 5. HWID 등록 (첫 로그인 시)
        if (string.IsNullOrEmpty(user.Hwid))
        {
            await _userService.RegisterHwidAsync(user, request.Hwid);
        }

        // 6. 세션 생성
        var userAgent = Request.Headers.UserAgent.ToString();
        var session = await _sessionService.CreateSessionAsync(user, request.Hwid, ipAddress, userAgent);

        // 7. 토큰 생성
        var accessToken = _jwtService.GenerateAccessToken(user);

        // 8. 마지막 로그인 업데이트
        await _userService.UpdateLastLoginAsync(user);

        // 9. 감사 로그
        await _auditService.LogLoginSuccessAsync(user.Id, ipAddress, request.Hwid);

        var expiresHours = _configuration.GetValue<int>("Jwt:AccessTokenExpiresHours", 24);

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = session.RefreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresHours * 3600,
            User = new UserInfo
            {
                Uid = user.Uid,
                Email = user.Email,
                LicenseStatus = user.LicenseStatus,
                LicenseExpiresAt = user.LicenseExpiresAt,
                IsAdmin = user.IsAdmin
            }
        };

        _logger.LogInformation("User {Email} logged in successfully", user.Email);

        return Ok(ApiResponse<LoginResponse>.Ok(response));
    }

    /// <summary>
    /// 토큰 갱신
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<RefreshTokenResponse>>> Refresh([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = GetClientIpAddress();

        // 1. 세션 조회
        var session = await _sessionService.GetByRefreshTokenAsync(request.RefreshToken);
        if (session == null)
        {
            return Unauthorized(ApiResponse<RefreshTokenResponse>.Fail(AuthErrorCode.AUTH_005_REFRESH_TOKEN_INVALID));
        }

        // 2. 세션 유효성 검증
        if (!_sessionService.ValidateSession(session))
        {
            return Unauthorized(ApiResponse<RefreshTokenResponse>.Fail(AuthErrorCode.AUTH_004_REFRESH_TOKEN_EXPIRED));
        }

        // 3. 라이선스 상태 재검증
        var licenseValidation = _userService.ValidateLicense(session.User);
        if (!licenseValidation.IsValid)
        {
            await _sessionService.DeactivateSessionAsync(session);
            return Unauthorized(ApiResponse<RefreshTokenResponse>.Fail(AuthErrorCode.LIC_001_LICENSE_EXPIRED));
        }

        // 4. 세션 갱신
        session = await _sessionService.RefreshSessionAsync(session);

        // 5. 새 Access Token 생성
        var accessToken = _jwtService.GenerateAccessToken(session.User);

        // 6. 감사 로그
        await _auditService.LogTokenRefreshAsync(session.UserId, ipAddress);

        var expiresHours = _configuration.GetValue<int>("Jwt:AccessTokenExpiresHours", 24);

        var response = new RefreshTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = session.RefreshToken,
            TokenType = "Bearer",
            ExpiresIn = expiresHours * 3600
        };

        return Ok(ApiResponse<RefreshTokenResponse>.Ok(response));
    }

    /// <summary>
    /// 로그아웃
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        var ipAddress = GetClientIpAddress();
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(ApiResponse<object>.Fail(AuthErrorCode.AUTH_003_TOKEN_INVALID));
        }

        // 세션 비활성화
        await _sessionService.DeactivateUserSessionsAsync(userId.Value);

        // 감사 로그
        await _auditService.LogLogoutAsync(userId.Value, ipAddress);

        return Ok(ApiResponse<object>.Ok(null));
    }

    private string? GetClientIpAddress()
    {
        // X-Forwarded-For 헤더 확인 (프록시/로드밸런서 뒤에 있는 경우)
        var forwardedFor = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',').FirstOrDefault()?.Trim();
        }

        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        return null;
    }
}
