namespace Rates.BuildingBlocks.Domain;

/// <summary>
/// Маркер для любой сущности, отслеживаемой репозиторием. Сущности обладают идентичностью
/// и жизненным циклом; они не взаимозаменяемы, в отличие от объектов-значений.
/// </summary>
public abstract class Entity<TId>(TId id)
    where TId : notnull
{
    public TId Id { get; protected set; } = id;

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id!);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}

/// <summary>
/// Корневой агрегат — сущность, владеющая транзакционной границей согласованности.
/// </summary>
public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id)
    where TId : notnull;
