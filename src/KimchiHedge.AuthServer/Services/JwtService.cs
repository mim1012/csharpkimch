using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using KimchiHedge.AuthServer.Entities;
using Microsoft.IdentityModel.Tokens;

namespace KimchiHedge.AuthServer.Services;

/// <summary>
/// JWT 토큰 서비스
/// </summary>
public class JwtService
{
    private readonly IConfiguration _configuration;
    private readonly byte[] _secretKey;

    public JwtService(IConfiguration configuration)
    {
        _configuration = configuration;
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("JWT Secret is not configured");
        _secretKey = Encoding.UTF8.GetBytes(secret);
    }

    /// <summary>
    /// Access Token 생성
    /// </summary>
    public string GenerateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("uid", user.Uid),
            new("license_status", user.LicenseStatus.ToString()),
            new(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };

        var expiresHours = _configuration.GetValue<int>("Jwt:AccessTokenExpiresHours", 24);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(expiresHours),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(_secretKey),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>
    /// Refresh Token 생성 (랜덤 문자열)
    /// </summary>
    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    /// <summary>
    /// Refresh Token 해시 생성 (SHA256 + Salt)
    /// </summary>
    /// <param name="token">평문 토큰</param>
    /// <returns>해시값과 솔트 튜플</returns>
    public (string Hash, string Salt) HashRefreshToken(string token)
    {
        var saltBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        var salt = Convert.ToBase64String(saltBytes);

        var hash = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(token + salt)));

        return (hash, salt);
    }

    /// <summary>
    /// Refresh Token 검증
    /// </summary>
    /// <param name="token">평문 토큰</param>
    /// <param name="hash">저장된 해시</param>
    /// <param name="salt">저장된 솔트</param>
    /// <returns>일치 여부</returns>
    public bool VerifyRefreshToken(string token, string hash, string salt)
    {
        var computed = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(token + salt)));
        return hash == computed;
    }

    /// <summary>
    /// Access Token 검증 및 ClaimsPrincipal 반환
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(_secretKey),
            ValidateIssuer = true,
            ValidIssuer = _configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = _configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 토큰에서 사용자 ID 추출
    /// </summary>
    public Guid? GetUserIdFromToken(string token)
    {
        var principal = ValidateToken(token);
        var userIdClaim = principal?.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }

        return null;
    }
}
