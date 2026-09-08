namespace Rates.BuildingBlocks.Application;

/// <summary>
/// Универсальный порт для коммита накопленных в репозитории изменений в БД. Реализации
/// могут использовать EF Core, Dapper или прямые SQL-запросы — контракт сводится к
/// асинхронному сохранению и возврату числа затронутых строк.
/// </summary>
public interface IUnitOfWorkFactory
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
