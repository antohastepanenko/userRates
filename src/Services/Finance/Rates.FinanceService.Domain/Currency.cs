using Rates.BuildingBlocks.Domain;

namespace Rates.FinanceService.Domain;

/// <summary>
/// Корневой агрегат, представляющий одну запись курса валюты.
/// </summary>
public sealed class Currency : AggregateRoot<Guid>
{
    private Currency(Guid id, string code, string name, decimal rate, decimal nominal,
        DateOnly rateDate, DateTimeOffset updatedAt)
        : base(id)
    {
        Code = code;
        Name = name;
        Rate = rate;
        Nominal = nominal;
        RateDate = rateDate;
        UpdatedAt = updatedAt;
    }

    private Currency()
        : base(Guid.Empty)
    {
    }

    /// <summary>Трёхбуквенный код ISO 4217, например <c>USD</c>.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Курс за единицу валюты по отношению к RUB.</summary>
    public decimal Rate { get; private set; }

    /// <summary>Количество единиц валюты, для которого указан курс (по умолчанию 1; ЦБ РФ часто использует 10 или 100).</summary>
    public decimal Nominal { get; private set; }

    public DateOnly RateDate { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static Currency Upsert(string code, string name, decimal rate, decimal nominal,
        DateOnly rateDate, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalizedCode = code.Trim().ToUpperInvariant();
        if (normalizedCode.Length != 3)
        {
            throw new ArgumentException("Currency code must be exactly 3 characters.", nameof(code));
        }

        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "Rate must be positive.");
        }

        if (nominal <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominal), nominal, "Nominal must be positive.");
        }

        return new Currency(Guid.NewGuid(), normalizedCode, name.Trim(), rate, nominal, rateDate, now);
    }

    public void Update(decimal rate, decimal nominal, DateOnly rateDate, DateTimeOffset now)
    {
        if (rate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rate), rate, "Rate must be positive.");
        }

        if (nominal <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(nominal), nominal, "Nominal must be positive.");
        }

        Rate = rate;
        Nominal = nominal;
        RateDate = rateDate;
        UpdatedAt = now;
    }
}