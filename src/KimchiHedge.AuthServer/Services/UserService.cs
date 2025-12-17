using KimchiHedge.AuthServer.Data;
using KimchiHedge.AuthServer.Entities;
using KimchiHedge.Core.Auth.Enums;
using Microsoft.EntityFrameworkCore;

namespace KimchiHedge.AuthServer.Services;

/// <summary>
/// 사용자 서비스
/// </summary>
public class UserService
{
    private readonly AuthDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(AuthDbContext context, ILogger<UserService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// 이메일로 사용자 조회
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    /// <summary>
    /// ID로 사용자 조회
    /// </summary>
    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    /// <summary>
    /// UID로 사용자 조회
    /// </summary>
    public async Task<User?> GetByUidAsync(string uid)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Uid == uid);
    }

    /// <summary>
    /// 비밀번호 검증
    /// </summary>
    public bool VerifyPassword(User user, string password)
    {
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    /// <summary>
    /// 비밀번호 해시
    /// </summary>
    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    /// <summary>
    /// HWID 검증
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateHwid(User user, string requestedHwid)
    {
        // 첫 로그인 - HWID 등록
        if (string.IsNullOrEmpty(user.Hwid))
        {
            return (true, null);
        }

        // HWID 일치 확인
        if (user.Hwid != requestedHwid)
        {
            return (false, "HWID mismatch. Contact administrator to reset.");
        }

        return (true, null);
    }

    /// <summary>
    /// HWID 등록
    /// </summary>
    public async Task RegisterHwidAsync(User user, string hwid)
    {
        if (string.IsNullOrEmpty(user.Hwid))
        {
            user.Hwid = hwid;
            user.HwidRegisteredAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("HWID registered for user {UserId}: {Hwid}", user.Id, hwid);
        }
    }

    /// <summary>
    /// HWID 리셋
    /// </summary>
    public async Task ResetHwidAsync(User user)
    {
        user.Hwid = null;
        user.HwidRegisteredAt = null;
        await _context.SaveChangesAsync();

        _logger.LogInformation("HWID reset for user {UserId}", user.Id);
    }

    /// <summary>
    /// 라이선스 상태 검증
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateLicense(User user)
    {
        switch (user.LicenseStatus)
        {
            case LicenseStatus.Pending:
                return (false, "Account pending approval");

            case LicenseStatus.Suspended:
                return (false, "Account suspended");

            case LicenseStatus.Expired:
                return (false, "License expired");

            case LicenseStatus.Active:
                // 만료일 확인
                if (user.LicenseExpiresAt.HasValue && user.LicenseExpiresAt.Value < DateTime.UtcNow)
                {
                    user.LicenseStatus = LicenseStatus.Expired;
                    _context.SaveChanges();
                    return (false, "License expired");
                }
                return (true, null);

            default:
                return (false, "Invalid license status");
        }
    }

    /// <summary>
    /// 마지막 로그인 업데이트
    /// </summary>
    public async Task UpdateLastLoginAsync(User user)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// 전체 사용자 목록 조회 (관리자용)
    /// </summary>
    public async Task<List<User>> GetAllUsersAsync()
    {
        return await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// 사용자 승인
    /// </summary>
    public async Task ApproveUserAsync(User user, int licenseDays = 30)
    {
        user.LicenseStatus = LicenseStatus.Active;
        user.LicenseExpiresAt = DateTime.UtcNow.AddDays(licenseDays);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} approved with {Days} days license", user.Id, licenseDays);
    }

    /// <summary>
    /// 사용자 정지
    /// </summary>
    public async Task SuspendUserAsync(User user)
    {
        user.LicenseStatus = LicenseStatus.Suspended;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("User {UserId} suspended", user.Id);
    }

    /// <summary>
    /// 라이선스 연장
    /// </summary>
    public async Task ExtendLicenseAsync(User user, int additionalDays)
    {
        var baseDate = user.LicenseExpiresAt ?? DateTime.UtcNow;
        if (baseDate < DateTime.UtcNow)
        {
            baseDate = DateTime.UtcNow;
        }

        user.LicenseExpiresAt = baseDate.AddDays(additionalDays);
        user.LicenseStatus = LicenseStatus.Active;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("License extended for user {UserId} by {Days} days", user.Id, additionalDays);
    }

    /// <summary>
    /// 사용자 생성 (회원가입)
    /// </summary>
    public async Task<User> CreateUserAsync(string email, string password, string? referralUid)
    {
        // 이메일 중복 체크
        var existingUser = await GetByEmailAsync(email);
        if (existingUser != null)
        {
            throw new InvalidOperationException("이미 등록된 이메일입니다.");
        }

        // 다음 UID 생성
        var uid = await GenerateNextUidAsync();

        // 비밀번호 해시
        var passwordHash = HashPassword(password);

        // 사용자 생성
        var user = new User
        {
            Id = Guid.NewGuid(),
            Uid = uid,
            Email = email.ToLower().Trim(),
            PasswordHash = passwordHash,
            LicenseStatus = LicenseStatus.Pending,
            ReferralUid = referralUid?.Trim(),
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New user registered: {Email} with UID {Uid}", email, uid);

        return user;
    }

    /// <summary>
    /// 이메일 존재 여부 확인
    /// </summary>
    public async Task<bool> EmailExistsAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email.ToLower() == email.ToLower());
    }

    /// <summary>
    /// 다음 UID 생성
    /// </summary>
    public async Task<string> GenerateNextUidAsync()
    {
        var lastUser = await _context.Users
            .OrderByDescending(u => u.Uid)
            .FirstOrDefaultAsync();

        if (lastUser == null || string.IsNullOrEmpty(lastUser.Uid))
        {
            return "USR-001";
        }

        // USR-XXX 형식에서 숫자 추출
        var parts = lastUser.Uid.Split('-');
        if (parts.Length == 2 && int.TryParse(parts[1], out var number))
        {
            return $"USR-{(number + 1):D3}";
        }

        return $"USR-{await _context.Users.CountAsync() + 1:D3}";
    }
}
