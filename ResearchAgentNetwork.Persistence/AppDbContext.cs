using Microsoft.EntityFrameworkCore;
using ResearchAgentNetwork;

namespace ResearchAgentNetwork.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ResearchTask> ResearchTasks => Set<ResearchTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResearchTask>(b =>
        {
            b.HasKey(t => t.Id);
            b.Property(t => t.Description).HasMaxLength(4096);
            b.Property(t => t.Priority);
            b.Property(t => t.Status);
            b.Ignore(t => t.Result); // wire later with owned entity or separate table
            b.Ignore(t => t.Metadata);
            b.Property(t => t.CreatedAt);
        });
    }
}