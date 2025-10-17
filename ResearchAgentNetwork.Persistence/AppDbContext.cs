using Microsoft.EntityFrameworkCore;
using ResearchAgentNetwork;
using ResearchAgentNetwork.Persistence.Entities;

namespace ResearchAgentNetwork.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
    public DbSet<TaskEventEntity> TaskEvents => Set<TaskEventEntity>();
    public DbSet<TaskReportEntity> TaskReports => Set<TaskReportEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<RefreshTokenEntity> RefreshTokens => Set<RefreshTokenEntity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Suppress pending model changes warning - this can happen with SQLite provider
        // when there are minor schema differences that don't affect functionality
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TaskEntity>(b =>
        {
            b.ToTable("Tasks");
            b.HasKey(t => t.Id);
            b.Property(t => t.Description).HasMaxLength(4096);
            b.Property(t => t.Priority);
            b.Property(t => t.Status);
            b.Property(t => t.CreatedAtUtc);
            b.Property(t => t.ParentTaskId);
            b.Property(t => t.UpdatedAtUtc);
            b.HasIndex(t => t.CreatedAtUtc);
        });

        modelBuilder.Entity<TaskEventEntity>(b =>
        {
            b.ToTable("TaskEvents");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.TaskId);
            b.HasIndex(e => e.TimestampUtc);
            b.HasIndex(e => new { e.TaskId, e.TimestampUtc });
            b.Property(e => e.EventType).HasMaxLength(64);
            b.Property(e => e.Status).HasMaxLength(32);
            b.Property(e => e.Message);
            b.Property(e => e.TimestampUtc);
            b.Property(e => e.AgentRole).HasMaxLength(64);
            b.Property(e => e.DetailsJson);
        });

        modelBuilder.Entity<TaskReportEntity>(b =>
        {
            b.ToTable("TaskReports");
            b.HasKey(r => r.TaskId);
            b.Property(r => r.ReportMarkdown);
            b.Property(r => r.GeneratedAtUtc);
        });

        modelBuilder.Entity<UserEntity>(b =>
        {
            b.ToTable("Users");
            b.HasKey(u => u.Id);
            b.Property(u => u.UserName).HasMaxLength(256);
            b.Property(u => u.NormalizedUserName).HasMaxLength(256);
            b.Property(u => u.Email).HasMaxLength(256);
            b.Property(u => u.NormalizedEmail).HasMaxLength(256);
            b.Property(u => u.PasswordHash);
            b.Property(u => u.DisplayName).HasMaxLength(256);
            b.Property(u => u.SecurityStamp).HasMaxLength(256);
            b.Property(u => u.CreatedAtUtc);
            b.Property(u => u.UpdatedAtUtc);
            b.HasIndex(u => u.NormalizedUserName).IsUnique();
            b.HasIndex(u => u.NormalizedEmail);
        });

        modelBuilder.Entity<RoleEntity>(b =>
        {
            b.ToTable("Roles");
            b.HasKey(r => r.Id);
            b.Property(r => r.Name).HasMaxLength(128);
            b.Property(r => r.NormalizedName).HasMaxLength(128);
            b.HasIndex(r => r.NormalizedName).IsUnique();
        });

        modelBuilder.Entity<UserRoleEntity>(b =>
        {
            b.ToTable("UserRoles");
            b.HasKey(ur => new { ur.UserId, ur.RoleId });
            b.HasOne(ur => ur.User).WithMany(u => u.Roles).HasForeignKey(ur => ur.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(ur => ur.Role).WithMany(r => r.Users).HasForeignKey(ur => ur.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RefreshTokenEntity>(b =>
        {
            b.ToTable("RefreshTokens");
            b.HasKey(rt => rt.Id);
            b.Property(rt => rt.Token).HasMaxLength(512);
            b.Property(rt => rt.CreatedAtUtc);
            b.Property(rt => rt.ExpiresAtUtc);
            b.Property(rt => rt.RevokedAtUtc);
            b.Property(rt => rt.ReplacedByToken).HasMaxLength(512);
            b.HasIndex(rt => new { rt.UserId, rt.Token }).IsUnique();
            b.HasOne(rt => rt.User).WithMany(u => u.RefreshTokens).HasForeignKey(rt => rt.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}