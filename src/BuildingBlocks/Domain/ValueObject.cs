namespace Rates.BuildingBlocks.Domain;

/// <summary>
/// Базовый класс для объектов-значений. Два объекта-значения считаются равными,
/// если совпадают их компоненты.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Компоненты, участвующие в сравнении и хэшировании.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj) =>
        obj is not null
        && obj.GetType() == GetType()
        && GetEqualityComponents().SequenceEqual(((ValueObject)obj).GetEqualityComponents());

    public override int GetHashCode() =>
        GetEqualityComponents()
            .Aggregate(1, (current, component) =>
                HashCode.Combine(current, component?.GetHashCode() ?? 0));

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}