namespace JobEngine.Application.interfaces;

public interface ILeaseManager
{
    Task<bool> TryAcquireAsync(Guid jobId, Guid token, TimeSpan ttl);
    Task<bool> RenewAsync(Guid jobId, Guid token, TimeSpan ttl);
    Task ReleaseAsync(Guid jobId, Guid token);
    Task<bool> ExistsAsync(Guid jobId);
}