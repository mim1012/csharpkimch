using KimchiHedge.AuthServer.Entities;
using KimchiHedge.Core.Auth.Enums;
using Microsoft.EntityFrameworkCore;

namespace KimchiHedge.AuthServer.Data;

/// <summary>
/// 인증 데이터베이스 컨텍스트
/// </summary>
public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PostgreSQL snake_case 테이블명
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Session>().ToTable("sessions");
        modelBuilder.Entity<AuditLog>().ToTable("audit_logs");

        // User 설정
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Uid).IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Uid).HasColumnName("uid").HasMaxLength(20);
            entity.Property(e => e.Email).HasColumnName("email").HasMaxLength(255);
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash").HasMaxLength(255);
            entity.Property(e => e.LicenseStatus).HasColumnName("license_status")
                  .HasConversion<string>()
                  .HasMaxLength(20);
            entity.Property(e => e.LicenseExpiresAt).HasColumnName("license_expires_at");
            entity.Property(e => e.Hwid).HasColumnName("hwid").HasMaxLength(64);
            entity.Property(e => e.HwidRegisteredAt).HasColumnName("hwid_registered_at");
            entity.Property(e => e.ReferralUid).HasColumnName("referral_uid").HasMaxLength(20);
            entity.Property(e => e.IsAdmin).HasColumnName("is_admin");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");
        });

        // Session 설정
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.IsActive });

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.RefreshTokenHash).HasColumnName("refresh_token_hash").HasMaxLength(88);
            entity.Property(e => e.Salt).HasColumnName("salt").HasMaxLength(44);
            entity.Property(e => e.Hwid).HasColumnName("hwid").HasMaxLength(64);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.IsActive).HasColumnName("is_active");

            entity.HasOne(e => e.User)
                  .WithMany(u => u.Sessions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // AuditLog 설정
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.Action);

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Action).HasColumnName("action").HasMaxLength(50);
            entity.Property(e => e.Result).HasColumnName("result").HasMaxLength(20);
            entity.Property(e => e.IpAddress).HasColumnName("ip_address").HasMaxLength(45);
            entity.Property(e => e.Hwid).HasColumnName("hwid").HasMaxLength(64);
            entity.Property(e => e.Details).HasColumnName("details");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
    }
}
