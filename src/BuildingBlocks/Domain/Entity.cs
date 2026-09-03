namespace Rates.BuildingBlocks.Domain;

/// <summary>
/// Маркер для любой сущности, отслеживаемой <see cref="IRepository{TEntity}"/>.
/// Сущности обладают идентичностью и жизненным циклом; они не взаимозаменяемы,
/// в отличие от объектов-значений.
/// </summary>
public abstract class Entity<TId>
    where TId : notnull
{
    protected Entity(TId id)
    {
        Id = id;
    }

    /// <summary>Суррогатный идентификатор. Доменные события не участвуют в сравнении.</summary>
    public TId Id { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = new();

    /// <summary>
    /// Доменные события, поднятые этим агрегатом в рамках текущей единицы работы.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent @event)
    {
        ArgumentNullException.ThrowIfNull(@event);
        _domainEvents.Add(@event);
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }

    public override bool Equals(object? obj) =>
        obj is Entity<TId> other && EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override int GetHashCode() => EqualityComparer<TId>.Default.GetHashCode(Id!);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}

/// <summary>
/// Маркер для корневых агрегатов — сущностей, владеющих транзакционной границей согласованности.
/// </summary>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id)
        : base(id)
    {
    }
}