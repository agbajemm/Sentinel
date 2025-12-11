using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SentinelAI.Core.Entities;
using SentinelAI.Core.Interfaces;
using SentinelAI.Infrastructure.Data;

namespace SentinelAI.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation
/// </summary>
public class Repository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly SentinelDbContext _context;
    protected readonly DbSet<T> _dbSet;
    
    public Repository(SentinelDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }
    
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }
    
    public virtual async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }
    
    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }
    
    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }
    
    public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }
    
    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        return predicate == null 
            ? await _dbSet.CountAsync(cancellationToken)
            : await _dbSet.CountAsync(predicate, cancellationToken);
    }
    
    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entry = await _dbSet.AddAsync(entity, cancellationToken);
        return entry.Entity;
    }
    
    public virtual async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        await _dbSet.AddRangeAsync(entities, cancellationToken);
        return entities;
    }
    
    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }
    
    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }
    
    public virtual async Task SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            entity.IsDeleted = true;
            entity.UpdatedAt = DateTime.UtcNow;
            await UpdateAsync(entity, cancellationToken);
        }
    }
}

/// <summary>
/// Unit of Work implementation
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly SentinelDbContext _context;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? _transaction;
    
    private IRepository<Tenant>? _tenants;
    private IRepository<TenantUser>? _tenantUsers;
    private IRepository<TransactionAnalysis>? _transactionAnalyses;
    private IRepository<FraudAlert>? _fraudAlerts;
    private IRepository<AuditLog>? _auditLogs;
    private IRepository<Core.Entities.AgentConfiguration>? _agentConfigurations;
    private IRepository<AgentInvocationLog>? _agentInvocationLogs;
    private IRepository<AgentMessage>? _agentMessages;
    
    public UnitOfWork(SentinelDbContext context)
    {
        _context = context;
    }
    
    public IRepository<Tenant> Tenants => 
        _tenants ??= new Repository<Tenant>(_context);
    
    public IRepository<TenantUser> TenantUsers => 
        _tenantUsers ??= new Repository<TenantUser>(_context);
    
    public IRepository<TransactionAnalysis> TransactionAnalyses => 
        _transactionAnalyses ??= new Repository<TransactionAnalysis>(_context);
    
    public IRepository<FraudAlert> FraudAlerts => 
        _fraudAlerts ??= new Repository<FraudAlert>(_context);
    
    public IRepository<AuditLog> AuditLogs => 
        _auditLogs ??= new Repository<AuditLog>(_context);
    
    public IRepository<Core.Entities.AgentConfiguration> AgentConfigurations => 
        _agentConfigurations ??= new Repository<Core.Entities.AgentConfiguration>(_context);
    
    public IRepository<AgentInvocationLog> AgentInvocationLogs => 
        _agentInvocationLogs ??= new Repository<AgentInvocationLog>(_context);
    
    public IRepository<AgentMessage> AgentMessages => 
        _agentMessages ??= new Repository<AgentMessage>(_context);
    
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
    
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }
    
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
    
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
    
    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
