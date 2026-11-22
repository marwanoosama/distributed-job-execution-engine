using JobEngine.Domain;
using JobEngine.Domain.models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace JobEngine.Application.interfaces;

public interface IJobDbContext
{
    DbSet<Job> Jobs { get; }
    DbSet<Worker> Workers { get; }
    DbSet<JobAttempt> JobAttempts { get; }
    
    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
