using KimchiHedge.AuthServer.Data;
using KimchiHedge.AuthServer.Entities;
using Microsoft.EntityFrameworkCore;

namespace KimchiHedge.AuthServer.Services;

/// <summary>
/// 세션 서비스
/// </summary>
public class SessionService
{
    private readonly AuthDbContext _context;
    private readonly JwtService _jwtService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SessionService> _logger;

    public SessionService(
        AuthDbContext context,
        JwtService jwtService,
        IConfiguration configuration,
        ILogger<SessionService> logger)
    {
        _context = context;
        _jwtService = jwtService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 새 세션 생성
    /// </summary>
    public async Task<Session> CreateSessionAsync(
        User user,
        string hwid,
        string? ipAddress,
        string? userAgent)
    {
        // 기존 활성 세션 비활성화 (단일 세션 정책)
        await DeactivateUserSessionsAsync(user.Id);

        var refreshTokenDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpiresDays", 30);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshToken = _jwtService.GenerateRefreshToken(),
            Hwid = hwid,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays),
            IsActive = true
        };

        _context.Sessions.Add(session);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Session created for user {UserId}", user.Id);

        return session;
    }

    /// <summary>
    /// Refresh Token으로 세션 조회
    /// </summary>
    public async Task<Session?> GetByRefreshTokenAsync(string refreshToken)
    {
        return await _context.Sessions
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.RefreshToken == refreshToken && s.IsActive);
    }

    /// <summary>
    /// 세션 유효성 검증
    /// </summary>
    public bool ValidateSession(Session session)
    {
        if (!session.IsActive)
        {
            return false;
        }

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// 세션 갱신 (새 Refresh Token 발급)
    /// </summary>
    public async Task<Session> RefreshSessionAsync(Session session)
    {
        var refreshTokenDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpiresDays", 30);

        session.RefreshToken = _jwtService.GenerateRefreshToken();
        session.ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Session refreshed for user {UserId}", session.UserId);

        return session;
    }

    /// <summary>
    /// 세션 비활성화
    /// </summary>
    public async Task DeactivateSessionAsync(Session session)
    {
        session.IsActive = false;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Session deactivated for user {UserId}", session.UserId);
    }

    /// <summary>
    /// 사용자의 모든 세션 비활성화
    /// </summary>
    public async Task DeactivateUserSessionsAsync(Guid userId)
    {
        var sessions = await _context.Sessions
            .Where(s => s.UserId == userId && s.IsActive)
            .ToListAsync();

        foreach (var session in sessions)
        {
            session.IsActive = false;
        }

        await _context.SaveChangesAsync();

        if (sessions.Count > 0)
        {
            _logger.LogInformation("Deactivated {Count} sessions for user {UserId}",
                sessions.Count, userId);
        }
    }

    /// <summary>
    /// 만료된 세션 정리 (백그라운드 작업용)
    /// </summary>
    public async Task CleanupExpiredSessionsAsync()
    {
        var expiredSessions = await _context.Sessions
            .Where(s => s.IsActive && s.ExpiresAt < DateTime.UtcNow)
            .ToListAsync();

        foreach (var session in expiredSessions)
        {
            session.IsActive = false;
        }

        await _context.SaveChangesAsync();

        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired sessions", expiredSessions.Count);
        }
    }

    /// <summary>
    /// 사용자의 활성 세션 조회
    /// </summary>
    public async Task<Session?> GetActiveSessionAsync(Guid userId)
    {
        return await _context.Sessions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsActive);
    }
}
