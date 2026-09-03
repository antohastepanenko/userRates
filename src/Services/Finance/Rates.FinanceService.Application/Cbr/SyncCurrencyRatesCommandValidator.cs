using FluentValidation;

namespace Rates.FinanceService.Application.Cbr;

public sealed class SyncCurrencyRatesCommandValidator : AbstractValidator<SyncCurrencyRatesCommand>
{
    public SyncCurrencyRatesCommandValidator()
    {
        RuleFor(c => c.Rates)
            .NotNull()
            .WithErrorCode("rates_required")
            .WithMessage("Rates payload is required.");

        RuleFor(c => c.Rates!.RateDate)
            .NotEqual(default(DateOnly))
            .When(c => c.Rates is not null)
            .WithErrorCode("rate_date_required")
            .WithMessage("RateDate must be supplied by the CBR feed.");

        RuleFor(c => c.Rates!.Entries)
            .NotEmpty()
            .When(c => c.Rates is not null)
            .WithErrorCode("rates_empty")
            .WithMessage("Rates payload must contain at least one currency.");

        RuleForEach(c => c.Rates!.Entries)
            .ChildRules(entry =>
            {
                entry.RuleFor(e => e.CharCode).Length(3).Matches("^[A-Za-z]{3}$");
                entry.RuleFor(e => e.Nominal).GreaterThan(0);
                entry.RuleFor(e => e.Value).GreaterThan(0);
                entry.RuleFor(e => e.NormalizedRate).GreaterThan(0);
            })
            .When(c => c.Rates?.Entries is not null);
    }
}