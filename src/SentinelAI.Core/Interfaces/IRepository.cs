using System.Linq.Expressions;
using SentinelAI.Core.Entities;

namespace SentinelAI.Core.Interfaces;

/// <summary>
/// Generic repository interface following Repository pattern
/// </summary>
/// <typeparam name="T">Entity type</typeparam>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unit of Work pattern for transaction management
/// </summary>
public interface IUnitOfWork : IDisposable
{
    IRepository<Tenant> Tenants { get; }
    IRepository<TenantUser> TenantUsers { get; }
    IRepository<TransactionAnalysis> TransactionAnalyses { get; }
    IRepository<FraudAlert> FraudAlerts { get; }
    IRepository<AuditLog> AuditLogs { get; }
    IRepository<AgentConfiguration> AgentConfigurations { get; }
    IRepository<AgentInvocationLog> AgentInvocationLogs { get; }
    IRepository<AgentMessage> AgentMessages { get; }
    
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Tenant-specific repository with automatic tenant filtering
/// </summary>
public interface ITenantRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindAsync(Guid tenantId, Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
    Task<T> AddAsync(Guid tenantId, T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid tenantId, T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid tenantId, Guid id, CancellationToken cancellationToken = default);
}
