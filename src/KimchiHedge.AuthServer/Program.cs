using System.Text;
using KimchiHedge.AuthServer.Components;
using KimchiHedge.AuthServer.Data;
using KimchiHedge.AuthServer.Entities;
using KimchiHedge.AuthServer.Services;
using KimchiHedge.Core.Auth.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ===== Services Configuration =====

// DbContext - Supabase PostgreSQL
var connectionString = Environment.GetEnvironmentVariable("KIMCHI_DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("AuthDb");
if (string.IsNullOrEmpty(connectionString))
    throw new InvalidOperationException("Database connection string is not configured. Set KIMCHI_DATABASE_URL environment variable.");

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseNpgsql(connectionString));

// Application Services
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AuditService>();

// JWT Authentication - 환경변수 우선, appsettings fallback
var jwtSecret = Environment.GetEnvironmentVariable("KIMCHI_JWT_SECRET")
    ?? builder.Configuration["Jwt:Secret"];
if (string.IsNullOrEmpty(jwtSecret))
    throw new InvalidOperationException("JWT Secret is not configured. Set KIMCHI_JWT_SECRET environment variable.");

// Configuration에 주입 (JwtService가 일관되게 참조)
builder.Configuration["Jwt:Secret"] = jwtSecret;

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Controllers
builder.Services.AddControllers();

// Blazor Server + MudBlazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "KimchiHedge Auth API",
        Version = "v1",
        Description = "Authentication and License Management API"
    });

    // JWT Bearer 인증 설정
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter 'Bearer' followed by a space and the JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// CORS 정책
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        var allowedOrigins = Environment.GetEnvironmentVariable("KIMCHI_ALLOWED_ORIGINS")
            ?? "http://localhost:5000";
        policy.WithOrigins(allowedOrigins.Split(','))
              .WithMethods("GET", "POST")
              .WithHeaders("Authorization", "Content-Type");
    });

    // 개발 환경용 정책
    options.AddPolicy("Development", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ===== Database Initialization =====
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // 데이터베이스 생성
    context.Database.EnsureCreated();
    logger.LogInformation("Database initialized");

    // 초기 관리자 계정 생성 (환경변수 우선, appsettings fallback)
    if (!context.Users.Any())
    {
        var adminEmail = Environment.GetEnvironmentVariable("KIMCHI_ADMIN_EMAIL")
            ?? builder.Configuration["Admin:Email"];
        var adminPassword = Environment.GetEnvironmentVariable("KIMCHI_ADMIN_PASSWORD")
            ?? builder.Configuration["Admin:Password"];

        if (!string.IsNullOrEmpty(adminEmail) && !string.IsNullOrEmpty(adminPassword))
        {
            var adminUser = new User
            {
                Id = Guid.NewGuid(),
                Uid = "ADMIN-001",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword),
                LicenseStatus = LicenseStatus.Active,
                LicenseExpiresAt = DateTime.UtcNow.AddYears(10),
                IsAdmin = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.Add(adminUser);
            context.SaveChanges();

            logger.LogInformation("Admin user created: {Email}", adminUser.Email);
        }
        else
        {
            logger.LogWarning("No admin credentials configured. Set KIMCHI_ADMIN_EMAIL and KIMCHI_ADMIN_PASSWORD environment variables.");
        }
    }
}

// ===== Middleware Pipeline =====

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "KimchiHedge Auth API v1");
        options.RoutePrefix = string.Empty; // Swagger UI at root
    });
}

// 환경에 따른 CORS 정책 적용
app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

// Static files for Blazor
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Blazor - /admin 경로로 매핑
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();

// For integration testing
public partial class Program { }
