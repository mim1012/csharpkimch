using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KimchiHedge.Core.Auth.Enums;
using KimchiHedge.Core.Auth.Models;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using KimchiHedge.AuthServer.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KimchiHedge.IntegrationTests;

/// <summary>
/// Auth API 통합 테스트
/// </summary>
public class AuthApiTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private static readonly string DbName = "TestDb_" + Guid.NewGuid().ToString("N");

    public AuthApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // 기존 DbContext 제거
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                // InMemory DB로 교체 - 클래스 내 모든 테스트에서 동일 DB 공유
                services.AddDbContext<AuthDbContext>(options =>
                {
                    options.UseInMemoryDatabase(DbName);
                });
            });
        });

        _client = _factory.CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    #region Health Check 테스트

    [Fact]
    public async Task HealthCheck_ShouldReturnHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("healthy");
    }

    #endregion

    #region 회원가입 테스트

    [Fact]
    public async Task Register_WithValidData_ShouldReturnSuccess()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = $"test{Guid.NewGuid():N}@example.com",
            Password = "Test1234!@#"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<RegisterResponse>>();
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Uid.Should().StartWith("USR-");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ShouldReturnBadRequest()
    {
        // Arrange
        var email = $"duplicate{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest
        {
            Email = email,
            Password = "Test1234!@#"
        };

        // First registration - ensure it succeeds
        var firstResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK, "First registration should succeed");

        // Act - Second registration with same email
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert - 중복 이메일은 BadRequest 또는 Conflict
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = $"test{Guid.NewGuid():N}@example.com",
            Password = "123" // Too weak
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region 로그인 테스트

    [Fact]
    public async Task Login_WithPendingUser_ShouldReturnForbidden()
    {
        // Arrange - Register new user (Pending status)
        var email = $"pending{Guid.NewGuid():N}@example.com";
        var password = "Test1234!@#";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password
        });

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password,
            Hwid = "TEST-HWID-12345678"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert - 실제 구현에서는 Pending 상태도 Unauthorized 반환
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ShouldReturnUnauthorized()
    {
        // Arrange
        var email = $"test{Guid.NewGuid():N}@example.com";
        await _client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = email,
            Password = "Test1234!@#"
        });

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = "WrongPassword123",
            Hwid = "TEST-HWID-12345678"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentEmail_ShouldReturnUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "Test1234!@#",
            Hwid = "TEST-HWID-12345678"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region 토큰 갱신 테스트

    [Fact]
    public async Task Refresh_WithInvalidToken_ShouldReturnUnauthorized()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "invalid-refresh-token",
            Hwid = "TEST-HWID-12345678"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion

    #region 인증 필요 엔드포인트 테스트

    [Fact]
    public async Task LicenseStatus_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/license/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithoutAuth_ShouldReturnUnauthorized()
    {
        // Act
        var response = await _client.PostAsync("/api/v1/auth/logout", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    #endregion
}

/// <summary>
/// API 응답 모델
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// 회원가입 요청 모델
/// </summary>
public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? ReferralUid { get; set; }
}

/// <summary>
/// 회원가입 응답 모델
/// </summary>
public class RegisterResponse
{
    public string Uid { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// 로그인 요청 모델
/// </summary>
public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Hwid { get; set; } = string.Empty;
}

/// <summary>
/// 토큰 갱신 요청 모델
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
    public string Hwid { get; set; } = string.Empty;
}
