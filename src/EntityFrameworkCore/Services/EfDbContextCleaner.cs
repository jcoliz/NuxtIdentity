using Microsoft.EntityFrameworkCore;
using NuxtIdentity.Core.Abstractions;

namespace NuxtIdentity.EntityFrameworkCore.Services;

/// <summary>
/// Entity Framework Core implementation of IDbContextCleaner.
/// </summary>
/// <typeparam name="TContext">The DbContext type to clean.</typeparam>
public class EfDbContextCleaner<TContext>(TContext dbContext) : IDbContextCleaner
    where TContext : DbContext
{
    private readonly TContext _dbContext = dbContext;

    /// <summary>
    /// Clears the Entity Framework DbContext change tracker, stopping tracking of all entities.
    /// </summary>
    public void ClearChangeTracker()
    {
        _dbContext.ChangeTracker.Clear();
    }
}
