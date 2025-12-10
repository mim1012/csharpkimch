using KimchiHedge.AuthServer.Services;
using KimchiHedge.Core.Auth.Enums;
using KimchiHedge.Core.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimchiHedge.AuthServer.Controllers;

/// <summary>
/// 라이선스 API 컨트롤러
/// </summary>
[ApiController]
[Route("api/v1/license")]
[Authorize]
public class LicenseController : ControllerBase
{
    private readonly UserService _userService;
    private readonly AuditService _auditService;
    private readonly ILogger<LicenseController> _logger;

    public LicenseController(
        UserService userService,
        AuditService auditService,
        ILogger<LicenseController> logger)
    {
        _userService = userService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// 라이선스 상태 조회
    /// </summary>
    [HttpGet("status")]
    public async Task<ActionResult<ApiResponse<LicenseStatusResponse>>> GetStatus()
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<LicenseStatusResponse>.Fail(AuthErrorCode.AUTH_003_TOKEN_INVALID));
        }

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound(ApiResponse<LicenseStatusResponse>.Fail(AuthErrorCode.AUTH_003_TOKEN_INVALID));
        }

        var response = new LicenseStatusResponse
        {
            Uid = user.Uid,
            Status = user.LicenseStatus,
            ExpiresAt = user.LicenseExpiresAt,
            IsValid = user.LicenseStatus == LicenseStatus.Active &&
                      (!user.LicenseExpiresAt.HasValue || user.LicenseExpiresAt.Value > DateTime.UtcNow),
            DaysRemaining = user.LicenseExpiresAt.HasValue
                ? Math.Max(0, (int)(user.LicenseExpiresAt.Value - DateTime.UtcNow).TotalDays)
                : null
        };

        return Ok(ApiResponse<LicenseStatusResponse>.Ok(response));
    }

    /// <summary>
    /// 하트비트 (클라이언트 활성 상태 확인)
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<ActionResult<ApiResponse<HeartbeatResponse>>> Heartbeat([FromBody] HeartbeatRequest request)
    {
        var ipAddress = GetClientIpAddress();
        var userId = GetCurrentUserId();

        if (userId == null)
        {
            return Unauthorized(ApiResponse<HeartbeatResponse>.Fail(AuthErrorCode.AUTH_003_TOKEN_INVALID));
        }

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound(ApiResponse<HeartbeatResponse>.Fail(AuthErrorCode.AUTH_003_TOKEN_INVALID));
        }

        // HWID 검증
        var hwidValidation = _userService.ValidateHwid(user, request.Hwid);
        if (!hwidValidation.IsValid)
        {
            await _auditService.LogHwidMismatchAsync(user.Id, ipAddress, request.Hwid, user.Hwid);
            return Unauthorized(ApiResponse<HeartbeatResponse>.Fail(AuthErrorCode.HWID_001_MISMATCH));
        }

        // 라이선스 상태 검증
        var licenseValidation = _userService.ValidateLicense(user);
        if (!licenseValidation.IsValid)
        {
            var errorCode = user.LicenseStatus switch
            {
                LicenseStatus.Expired => AuthErrorCode.LIC_001_LICENSE_EXPIRED,
                LicenseStatus.Suspended => AuthErrorCode.LIC_002_LICENSE_SUSPENDED,
                _ => AuthErrorCode.LIC_001_LICENSE_EXPIRED
            };

            return Unauthorized(ApiResponse<HeartbeatResponse>.Fail(errorCode));
        }

        // 감사 로그 (하트비트는 매번 기록하지 않고 선택적으로)
        // await _auditService.LogHeartbeatAsync(user.Id, ipAddress, request.Hwid);

        var response = new HeartbeatResponse
        {
            ServerTime = DateTime.UtcNow,
            LicenseValid = true,
            LicenseStatus = user.LicenseStatus,
            LicenseExpiresAt = user.LicenseExpiresAt,
            NextHeartbeatSeconds = 300 // 5분
        };

        return Ok(ApiResponse<HeartbeatResponse>.Ok(response));
    }

    private string? GetClientIpAddress()
    {
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
