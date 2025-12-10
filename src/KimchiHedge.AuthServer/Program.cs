using System.Text;
using KimchiHedge.AuthServer.Data;
using KimchiHedge.AuthServer.Entities;
using KimchiHedge.AuthServer.Services;
using KimchiHedge.Core.Auth.Enums;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ===== Services Configuration =====

// DbContext
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("AuthDb")));

// Application Services
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<AuditService>();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Secret is not configured");
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

// CORS (개발용)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
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

    // Seed 데이터 생성
    if (!context.Users.Any())
    {
        var config = builder.Configuration;

        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Uid = "ADMIN-001",
            Email = config["Seed:AdminEmail"] ?? "admin@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(config["Seed:AdminPassword"] ?? "admin123"),
            LicenseStatus = LicenseStatus.Active,
            LicenseExpiresAt = DateTime.UtcNow.AddYears(10),
            IsAdmin = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var testUser = new User
        {
            Id = Guid.NewGuid(),
            Uid = "USR-001",
            Email = config["Seed:TestUserEmail"] ?? "user@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(config["Seed:TestUserPassword"] ?? "user123"),
            LicenseStatus = LicenseStatus.Active,
            LicenseExpiresAt = DateTime.UtcNow.AddMonths(1),
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Users.AddRange(adminUser, testUser);
        context.SaveChanges();

        logger.LogInformation("Seed data created:");
        logger.LogInformation("  Admin: {Email} / {Password}", adminUser.Email, config["Seed:AdminPassword"]);
        logger.LogInformation("  User: {Email} / {Password}", testUser.Email, config["Seed:TestUserPassword"]);
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

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
