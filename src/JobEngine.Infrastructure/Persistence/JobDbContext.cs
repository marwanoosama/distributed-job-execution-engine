using JobEngine.Domain;
using JobEngine.Domain.models;
using Microsoft.EntityFrameworkCore;

namespace JobEngine.Infrastructure.Persistence;

public class JobDbContext(DbContextOptions<JobDbContext> options) : DbContext(options)
{
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Worker> Workers => Set<Worker>();
    public DbSet<JobAttempt> JobAttempts => Set<JobAttempt>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("jobs");

            entity.Property(j => j.Type).HasMaxLength(200);
            entity.Property(j => j.Payload).HasColumnType("jsonb");
            entity.Property(j => j.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(j => j.RunAt).HasDefaultValueSql("now()");
            entity.Property(j => j.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(j => j.LastError).HasMaxLength(4000);

            entity.HasIndex(j => j.IdempotencyKey).IsUnique();
            entity.HasIndex(j => new { j.Status, j.RunAt });
            entity.HasIndex(j => j.Type);
        });

        modelBuilder.Entity<Worker>(entity =>
        {
            entity.ToTable("workers");

            entity.Property(w => w.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(w => w.StartedAt).HasDefaultValueSql("now()");
            entity.Property(w => w.LastHeartbeat).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<JobAttempt>(entity =>
        {
            entity.ToTable("job_attempts");

            entity.Property(a => a.Result).HasConversion<string>().HasMaxLength(20);
            entity.Property(a => a.StartedAt).HasDefaultValueSql("now()");

            entity.HasIndex(a => a.JobId);

            entity.HasOne(a => a.Job)
                  .WithMany()
                  .HasForeignKey(a => a.JobId);

            entity.HasOne(a => a.Worker)
                  .WithMany()
                  .HasForeignKey(a => a.WorkerId);
        });
    }
}
