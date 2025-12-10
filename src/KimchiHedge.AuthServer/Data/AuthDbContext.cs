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

        // User 설정
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasIndex(e => e.Uid).IsUnique();

            entity.Property(e => e.LicenseStatus)
                  .HasConversion<string>()
                  .HasMaxLength(20);

            entity.Property(e => e.Email).HasMaxLength(255);
            entity.Property(e => e.Uid).HasMaxLength(20);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.Hwid).HasMaxLength(64);
        });

        // Session 설정
        modelBuilder.Entity<Session>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RefreshToken);
            entity.HasIndex(e => new { e.UserId, e.IsActive });

            entity.Property(e => e.RefreshToken).HasMaxLength(255);
            entity.Property(e => e.Hwid).HasMaxLength(64);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasMaxLength(500);

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

            entity.Property(e => e.Action).HasMaxLength(50);
            entity.Property(e => e.Result).HasMaxLength(20);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.Hwid).HasMaxLength(64);
        });
    }
}
