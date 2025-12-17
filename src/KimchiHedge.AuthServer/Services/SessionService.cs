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
    /// <returns>세션과 평문 Refresh Token 튜플 (토큰은 클라이언트에게만 전달)</returns>
    public async Task<(Session Session, string RefreshToken)> CreateSessionAsync(
        User user,
        string hwid,
        string? ipAddress,
        string? userAgent)
    {
        // 기존 활성 세션 비활성화 (단일 세션 정책)
        await DeactivateUserSessionsAsync(user.Id);

        var refreshTokenDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpiresDays", 30);

        // 평문 토큰 생성
        var refreshToken = _jwtService.GenerateRefreshToken();

        // 해시 + 솔트 생성
        var (hash, salt) = _jwtService.HashRefreshToken(refreshToken);

        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshTokenHash = hash,
            Salt = salt,
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

        return (session, refreshToken);
    }

    /// <summary>
    /// Refresh Token으로 세션 조회 (해시 기반)
    /// </summary>
    public async Task<Session?> GetByRefreshTokenAsync(string refreshToken)
    {
        // 모든 활성 세션을 조회 후 해시 매칭
        var activeSessions = await _context.Sessions
            .Include(s => s.User)
            .Where(s => s.IsActive)
            .ToListAsync();

        foreach (var session in activeSessions)
        {
            if (_jwtService.VerifyRefreshToken(refreshToken, session.RefreshTokenHash, session.Salt))
            {
                return session;
            }
        }

        return null;
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
    /// <returns>세션과 새 평문 Refresh Token 튜플</returns>
    public async Task<(Session Session, string RefreshToken)> RefreshSessionAsync(Session session)
    {
        var refreshTokenDays = _configuration.GetValue<int>("Jwt:RefreshTokenExpiresDays", 30);

        // 새 평문 토큰 생성
        var refreshToken = _jwtService.GenerateRefreshToken();

        // 해시 + 솔트 생성
        var (hash, salt) = _jwtService.HashRefreshToken(refreshToken);

        session.RefreshTokenHash = hash;
        session.Salt = salt;
        session.ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenDays);

        await _context.SaveChangesAsync();

        _logger.LogInformation("Session refreshed for user {UserId}", session.UserId);

        return (session, refreshToken);
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
