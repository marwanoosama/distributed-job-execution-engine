using JobEngine.Domain;
using JobEngine.Domain.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace JobEngine.Application.interfaces;

public interface IJobDbContext
{
    DbSet<Job> Jobs { get; }
    DbSet<Worker> Workers { get; }
    DbSet<JobAttempt> JobAttempts { get; }
    
    DatabaseFacade Database { get; }
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity) where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
