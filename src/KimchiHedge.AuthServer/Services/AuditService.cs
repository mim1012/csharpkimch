using KimchiHedge.AuthServer.Data;
using KimchiHedge.AuthServer.Entities;

namespace KimchiHedge.AuthServer.Services;

/// <summary>
/// 감사 로그 서비스
/// </summary>
public class AuditService
{
    private readonly AuthDbContext _context;
    private readonly ILogger<AuditService> _logger;

    public AuditService(AuthDbContext context, ILogger<AuditService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 감사 로그 기록
    /// </summary>
    public async Task LogAsync(
        string action,
        string result,
        Guid? userId = null,
        string? ipAddress = null,
        string? hwid = null,
        string? details = null)
    {
        try
        {
            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = action,
                Result = result,
                IpAddress = ipAddress,
                Hwid = hwid,
                Details = details,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Audit: {Action} - {Result} - User: {UserId} - IP: {IpAddress}",
                action, result, userId, ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log: {Action}", action);
        }
    }

    /// <summary>
    /// 회원가입 기록
    /// </summary>
    public Task LogUserRegisteredAsync(Guid userId, string? ipAddress, string? referralUid)
        => LogAsync(AuditAction.Register, AuditResult.Success, userId, ipAddress, null,
            string.IsNullOrEmpty(referralUid) ? null : System.Text.Json.JsonSerializer.Serialize(new { referralUid }));

    /// <summary>
    /// 로그인 성공 기록
    /// </summary>
    public Task LogLoginSuccessAsync(Guid userId, string? ipAddress, string? hwid)
        => LogAsync(AuditAction.Login, AuditResult.Success, userId, ipAddress, hwid);

    /// <summary>
    /// 로그인 실패 기록
    /// </summary>
    public Task LogLoginFailedAsync(string? email, string? ipAddress, string? hwid, string reason)
        => LogAsync(AuditAction.LoginFailed, AuditResult.Failed, null, ipAddress, hwid,
            System.Text.Json.JsonSerializer.Serialize(new { email, reason }));

    /// <summary>
    /// 로그아웃 기록
    /// </summary>
    public Task LogLogoutAsync(Guid userId, string? ipAddress)
        => LogAsync(AuditAction.Logout, AuditResult.Success, userId, ipAddress);

    /// <summary>
    /// 토큰 갱신 기록
    /// </summary>
    public Task LogTokenRefreshAsync(Guid userId, string? ipAddress)
        => LogAsync(AuditAction.TokenRefresh, AuditResult.Success, userId, ipAddress);

    /// <summary>
    /// 하트비트 기록
    /// </summary>
    public Task LogHeartbeatAsync(Guid userId, string? ipAddress, string? hwid)
        => LogAsync(AuditAction.Heartbeat, AuditResult.Success, userId, ipAddress, hwid);

    /// <summary>
    /// HWID 불일치 기록
    /// </summary>
    public Task LogHwidMismatchAsync(Guid userId, string? ipAddress, string? requestedHwid, string? registeredHwid)
        => LogAsync(AuditAction.HwidMismatch, AuditResult.Failed, userId, ipAddress, requestedHwid,
            System.Text.Json.JsonSerializer.Serialize(new { requestedHwid, registeredHwid }));
}
