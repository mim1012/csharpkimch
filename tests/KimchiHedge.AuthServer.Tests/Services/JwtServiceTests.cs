using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using KimchiHedge.AuthServer.Entities;
using KimchiHedge.AuthServer.Services;
using KimchiHedge.Core.Auth.Enums;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KimchiHedge.AuthServer.Tests.Services;

/// <summary>
/// JwtService 단위 테스트
/// </summary>
public class JwtServiceTests
{
    private readonly JwtService _jwtService;
    private readonly IConfiguration _configuration;

    public JwtServiceTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "Jwt:Secret", "KimchiHedge2024SecretKeyAutoTrading1234567890" },
            { "Jwt:Issuer", "KimchiHedge.AuthServer" },
            { "Jwt:Audience", "KimchiHedge.Client" },
            { "Jwt:AccessTokenExpiresHours", "24" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _jwtService = new JwtService(_configuration);
    }

    #region Access Token 테스트

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidJwtToken()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var token = _jwtService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainUserClaims()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var token = _jwtService.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // JWT claims use short names (email, role) not full URIs
        jwtToken.Claims.Should().Contain(c =>
            (c.Type == ClaimTypes.Email || c.Type == "email") && c.Value == user.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == "uid" && c.Value == user.Uid);
        jwtToken.Claims.Should().Contain(c => c.Type == "license_status");
    }

    [Fact]
    public void GenerateAccessToken_ForAdmin_ShouldContainAdminRole()
    {
        // Arrange
        var adminUser = CreateTestUser(isAdmin: true);

        // Act
        var token = _jwtService.GenerateAccessToken(adminUser);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // JWT claims use short names (role) not full URIs
        jwtToken.Claims.Should().Contain(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == "Admin");
    }

    [Fact]
    public void GenerateAccessToken_ForRegularUser_ShouldContainUserRole()
    {
        // Arrange
        var user = CreateTestUser(isAdmin: false);

        // Act
        var token = _jwtService.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        // JWT claims use short names (role) not full URIs
        jwtToken.Claims.Should().Contain(c =>
            (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == "User");
    }

    #endregion

    #region Refresh Token 테스트

    [Fact]
    public void GenerateRefreshToken_ShouldReturnUniqueTokens()
    {
        // Act
        var token1 = _jwtService.GenerateRefreshToken();
        var token2 = _jwtService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBeNullOrEmpty();
        token2.Should().NotBeNullOrEmpty();
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnBase64String()
    {
        // Act
        var token = _jwtService.GenerateRefreshToken();

        // Assert
        var act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }

    #endregion

    #region Refresh Token 해시 테스트

    [Fact]
    public void HashRefreshToken_ShouldReturnHashAndSalt()
    {
        // Arrange
        var token = _jwtService.GenerateRefreshToken();

        // Act
        var (hash, salt) = _jwtService.HashRefreshToken(token);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        salt.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HashRefreshToken_SameToken_ShouldProduceDifferentHashes()
    {
        // Arrange
        var token = _jwtService.GenerateRefreshToken();

        // Act
        var (hash1, salt1) = _jwtService.HashRefreshToken(token);
        var (hash2, salt2) = _jwtService.HashRefreshToken(token);

        // Assert - 다른 솔트로 인해 다른 해시
        hash1.Should().NotBe(hash2);
        salt1.Should().NotBe(salt2);
    }

    [Fact]
    public void VerifyRefreshToken_WithCorrectToken_ShouldReturnTrue()
    {
        // Arrange
        var token = _jwtService.GenerateRefreshToken();
        var (hash, salt) = _jwtService.HashRefreshToken(token);

        // Act
        var result = _jwtService.VerifyRefreshToken(token, hash, salt);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyRefreshToken_WithWrongToken_ShouldReturnFalse()
    {
        // Arrange
        var token = _jwtService.GenerateRefreshToken();
        var wrongToken = _jwtService.GenerateRefreshToken();
        var (hash, salt) = _jwtService.HashRefreshToken(token);

        // Act
        var result = _jwtService.VerifyRefreshToken(wrongToken, hash, salt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyRefreshToken_WithWrongSalt_ShouldReturnFalse()
    {
        // Arrange
        var token = _jwtService.GenerateRefreshToken();
        var (hash, _) = _jwtService.HashRefreshToken(token);
        var (_, wrongSalt) = _jwtService.HashRefreshToken(token);

        // Act
        var result = _jwtService.VerifyRefreshToken(token, hash, wrongSalt);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Token 검증 테스트

    [Fact]
    public void ValidateToken_WithValidToken_ShouldReturnClaimsPrincipal()
    {
        // Arrange
        var user = CreateTestUser();
        var token = _jwtService.GenerateAccessToken(user);

        // Act
        var principal = _jwtService.ValidateToken(token);

        // Assert
        principal.Should().NotBeNull();
        principal!.Identity.Should().NotBeNull();
        principal.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        var invalidToken = "invalid.token.here";

        // Act
        var principal = _jwtService.ValidateToken(invalidToken);

        // Assert
        principal.Should().BeNull();
    }

    [Fact]
    public void ValidateToken_WithTamperedToken_ShouldReturnNull()
    {
        // Arrange
        var user = CreateTestUser();
        var token = _jwtService.GenerateAccessToken(user);
        var tamperedToken = token.Substring(0, token.Length - 5) + "XXXXX";

        // Act
        var principal = _jwtService.ValidateToken(tamperedToken);

        // Assert
        principal.Should().BeNull();
    }

    #endregion

    #region GetUserIdFromToken 테스트

    [Fact]
    public void GetUserIdFromToken_WithValidToken_ShouldReturnUserId()
    {
        // Arrange
        var user = CreateTestUser();
        var token = _jwtService.GenerateAccessToken(user);

        // Act
        var userId = _jwtService.GetUserIdFromToken(token);

        // Assert
        userId.Should().NotBeNull();
        userId.Should().Be(user.Id);
    }

    [Fact]
    public void GetUserIdFromToken_WithInvalidToken_ShouldReturnNull()
    {
        // Arrange
        var invalidToken = "invalid.token";

        // Act
        var userId = _jwtService.GetUserIdFromToken(invalidToken);

        // Assert
        userId.Should().BeNull();
    }

    #endregion

    #region 헬퍼 메서드

    private static User CreateTestUser(bool isAdmin = false)
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Uid = "USR-TEST01",
            Email = "test@example.com",
            PasswordHash = "hashedpassword",
            LicenseStatus = LicenseStatus.Active,
            IsAdmin = isAdmin,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion
}
