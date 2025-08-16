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
        });

        modelBuilder.Entity<TaskEventEntity>(b =>
        {
            b.ToTable("TaskEvents");
            b.HasKey(e => e.Id);
            b.HasIndex(e => e.TaskId);
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
    }
}