using MediatR;

namespace Rates.BuildingBlocks.Domain;

/// <summary>
/// Маркер для доменных событий. Доменные события представляют факты, которые уже произошли
/// в прошлом и представляют интерес для других частей системы.
/// </summary>
public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredAt { get; }
}

public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent(DateTimeOffset occurredAt)
    {
        OccurredAt = occurredAt;
    }

    public DateTimeOffset OccurredAt { get; }
}