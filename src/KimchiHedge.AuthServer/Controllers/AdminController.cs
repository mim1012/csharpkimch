using KimchiHedge.AuthServer.Entities;
using KimchiHedge.AuthServer.Services;
using KimchiHedge.Core.Auth.Enums;
using KimchiHedge.Core.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KimchiHedge.AuthServer.Controllers;

/// <summary>
/// 관리자 API 컨트롤러
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly UserService _userService;
    private readonly SessionService _sessionService;
    private readonly AuditService _auditService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        UserService userService,
        SessionService sessionService,
        AuditService auditService,
        ILogger<AdminController> logger)
    {
        _userService = userService;
        _sessionService = sessionService;
        _auditService = auditService;
        _logger = logger;
    }

    /// <summary>
    /// 전체 사용자 목록 조회
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<List<AdminUserDto>>>> GetUsers()
    {
        var users = await _userService.GetAllUsersAsync();

        var userDtos = users.Select(u => new AdminUserDto
        {
            Id = u.Id,
            Uid = u.Uid,
            Email = u.Email,
            LicenseStatus = u.LicenseStatus,
            LicenseExpiresAt = u.LicenseExpiresAt,
            Hwid = u.Hwid,
            HwidRegisteredAt = u.HwidRegisteredAt,
            IsAdmin = u.IsAdmin,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        }).ToList();

        return Ok(ApiResponse<List<AdminUserDto>>.Ok(userDtos));
    }

    /// <summary>
    /// 사용자 상세 조회
    /// </summary>
    [HttpGet("users/{uid}")]
    public async Task<ActionResult<ApiResponse<AdminUserDto>>> GetUser(string uid)
    {
        var user = await _userService.GetByUidAsync(uid);
        if (user == null)
        {
            return NotFound(ApiResponse<AdminUserDto>.Fail(AuthErrorCode.AUTH_001_INVALID_CREDENTIALS, "User not found"));
        }

        var dto = new AdminUserDto
        {
            Id = user.Id,
            Uid = user.Uid,
            Email = user.Email,
            LicenseStatus = user.LicenseStatus,
            LicenseExpiresAt = user.LicenseExpiresAt,
            Hwid = user.Hwid,
            HwidRegisteredAt = user.HwidRegisteredAt,
            IsAdmin = user.IsAdmin,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };

        return Ok(ApiResponse<AdminUserDto>.Ok(dto));
    }

    /// <summary>
    /// 사용자 승인
    /// </summary>
    [HttpPost("users/{uid}/approve")]
    public async Task<ActionResult<ApiResponse<object>>> ApproveUser(string uid, [FromBody] ApproveRequest? request = null)
    {
        var user = await _userService.GetByUidAsync(uid);
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Fail(AuthErrorCode.AUTH_001_INVALID_CREDENTIALS, "User not found"));
        }

        var licenseDays = request?.LicenseDays ?? 30;
        await _userService.ApproveUserAsync(user, licenseDays);

        await _auditService.LogAsync(
            AuditAction.UserApproved,
            AuditResult.Success,
            user.Id,
            GetClientIpAddress(),
            details: System.Text.Json.JsonSerializer.Serialize(new { licenseDays }));

        _logger.LogInformation("User {Uid} approved with {Days} days license", uid, licenseDays);

        return Ok(ApiResponse<object>.Ok(null));
    }

    /// <summary>
    /// 사용자 정지
    /// </summary>
    [HttpPost("users/{uid}/suspend")]
    public async Task<ActionResult<ApiResponse<object>>> SuspendUser(string uid)
    {
        var user = await _userService.GetByUidAsync(uid);
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Fail(AuthErrorCode.AUTH_001_INVALID_CREDENTIALS, "User not found"));
        }

        await _userService.SuspendUserAsync(user);

        // 활성 세션 종료
        await _sessionService.DeactivateUserSessionsAsync(user.Id);

        await _auditService.LogAsync(
            AuditAction.UserSuspended,
            AuditResult.Success,
            user.Id,
            GetClientIpAddress());

        _logger.LogInformation("User {Uid} suspended", uid);

        return Ok(ApiResponse<object>.Ok(null));
    }

    /// <summary>
    /// HWID 리셋
    /// </summary>
    [HttpPost("users/{uid}/reset-hwid")]
    public async Task<ActionResult<ApiResponse<object>>> ResetHwid(string uid)
    {
        var user = await _userService.GetByUidAsync(uid);
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Fail(AuthErrorCode.AUTH_001_INVALID_CREDENTIALS, "User not found"));
        }

        var oldHwid = user.Hwid;
        await _userService.ResetHwidAsync(user);

        await _auditService.LogAsync(
            AuditAction.HwidReset,
            AuditResult.Success,
            user.Id,
            GetClientIpAddress(),
            details: System.Text.Json.JsonSerializer.Serialize(new { oldHwid }));

        _logger.LogInformation("HWID reset for user {Uid}", uid);

        return Ok(ApiResponse<object>.Ok(null));
    }

    /// <summary>
    /// 라이선스 연장
    /// </summary>
    [HttpPost("users/{uid}/extend-license")]
    public async Task<ActionResult<ApiResponse<object>>> ExtendLicense(string uid, [FromBody] ExtendLicenseRequest request)
    {
        var user = await _userService.GetByUidAsync(uid);
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Fail(AuthErrorCode.AUTH_001_INVALID_CREDENTIALS, "User not found"));
        }

        await _userService.ExtendLicenseAsync(user, request.Days);

        await _auditService.LogAsync(
            AuditAction.LicenseExtended,
            AuditResult.Success,
            user.Id,
            GetClientIpAddress(),
            details: System.Text.Json.JsonSerializer.Serialize(new { days = request.Days, newExpiresAt = user.LicenseExpiresAt }));

        _logger.LogInformation("License extended for user {Uid} by {Days} days", uid, request.Days);

        return Ok(ApiResponse<object>.Ok(null));
    }

    /// <summary>
    /// 사용자 강제 로그아웃
    /// </summary>
    [HttpPost("users/{uid}/force-logout")]
    public async Task<ActionResult<ApiResponse<object>>> ForceLogout(string uid)
    {
        var user = await _userService.GetByUidAsync(uid);
        if (user == null)
        {
            return NotFound(ApiResponse<object>.Fail(AuthErrorCode.AUTH_001_INVALID_CREDENTIALS, "User not found"));
        }

        await _sessionService.DeactivateUserSessionsAsync(user.Id);

        _logger.LogInformation("Force logout for user {Uid}", uid);

        return Ok(ApiResponse<object>.Ok(null));
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
}

/// <summary>
/// 관리자용 사용자 DTO
/// </summary>
public class AdminUserDto
{
    public Guid Id { get; set; }
    public string Uid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public LicenseStatus LicenseStatus { get; set; }
    public DateTime? LicenseExpiresAt { get; set; }
    public string? Hwid { get; set; }
    public DateTime? HwidRegisteredAt { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// 승인 요청
/// </summary>
public class ApproveRequest
{
    public int LicenseDays { get; set; } = 30;
}

/// <summary>
/// 라이선스 연장 요청
/// </summary>
public class ExtendLicenseRequest
{
    public int Days { get; set; }
}
