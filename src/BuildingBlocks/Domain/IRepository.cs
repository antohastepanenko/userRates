namespace Rates.BuildingBlocks.Domain;

/// <summary>
/// Абстракция репозитория над корневым агрегатом. Реализация находится в Infrastructure,
/// поэтому domain-слой никогда не ссылается на EF Core напрямую.
/// </summary>
public interface IRepository<TEntity>
    where TEntity : Entity<Guid>
{
    Task<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    void Remove(TEntity entity);
}

/// <summary>
/// Абстракция единицы работы. Конкретная реализация находится в Infrastructure и обычно
/// оборачивает <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync(System.Threading.CancellationToken)"/>.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}