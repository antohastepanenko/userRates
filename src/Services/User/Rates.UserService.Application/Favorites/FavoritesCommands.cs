using FluentValidation;
using MediatR;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Favorites;

public sealed record FavoriteCurrencyView(string Code, DateTimeOffset AddedAt);

public sealed record GetFavoriteCurrenciesQuery : IQuery<Result<IReadOnlyList<FavoriteCurrencyView>>>;

public sealed class GetFavoriteCurrenciesQueryHandler : IRequestHandler<GetFavoriteCurrenciesQuery, Result<IReadOnlyList<FavoriteCurrencyView>>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IFavoritesRepository _favorites;

    public GetFavoriteCurrenciesQueryHandler(ICurrentUser currentUser, IFavoritesRepository favorites)
    {
        _currentUser = currentUser;
        _favorites = favorites;
    }

    public async Task<Result<IReadOnlyList<FavoriteCurrencyView>>> Handle(GetFavoriteCurrenciesQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return Result<IReadOnlyList<FavoriteCurrencyView>>.Failure(
                Error.Unauthorized("not_authenticated", "Authentication is required."));
        }

        var favorites = await _favorites.ListWithAddedAtAsync(_currentUser.UserId.Value, cancellationToken);
        var view = favorites
            .Select(f => new FavoriteCurrencyView(f.Code, f.AddedAt))
            .ToArray();
        return Result<IReadOnlyList<FavoriteCurrencyView>>.Ok(view);
    }
}

public sealed record AddFavoriteCurrencyCommand(string Code) : ICommand<Result<Unit>>;

public sealed class AddFavoriteCurrencyCommandValidator : AbstractValidator<AddFavoriteCurrencyCommand>
{
    public AddFavoriteCurrencyCommandValidator()
    {
        RuleFor(c => c.Code)
            .NotEmpty()
            .Length(3)
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Code must be three letters (ISO 4217).");
    }
}

public sealed class AddFavoriteCurrencyCommandHandler : IRequestHandler<AddFavoriteCurrencyCommand, Result<Unit>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IFavoritesRepository _favorites;
    private readonly IClock _clock;
    private readonly IUnitOfWorkFactory _unitOfWork;

    public AddFavoriteCurrencyCommandHandler(
        ICurrentUser currentUser,
        IFavoritesRepository favorites,
        IClock clock,
        IUnitOfWorkFactory unitOfWork)
    {
        _currentUser = currentUser;
        _favorites = favorites;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<Unit>> Handle(AddFavoriteCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return Result<Unit>.Failure(Error.Unauthorized("not_authenticated", "Authentication is required."));
        }

        var normalized = request.Code.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            return Result<Unit>.Failure(Error.Validation("invalid_currency_code", "Currency code must be 3 letters."));
        }

        await _favorites.AddIfMissingAsync(_currentUser.UserId.Value, normalized, _clock.UtcNow, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result<Unit>.Ok(Unit.Value);
    }
}

public sealed record RemoveFavoriteCurrencyCommand(string Code) : ICommand<Result<bool>>;

public sealed class RemoveFavoriteCurrencyCommandValidator : AbstractValidator<RemoveFavoriteCurrencyCommand>
{
    public RemoveFavoriteCurrencyCommandValidator()
    {
        RuleFor(c => c.Code).NotEmpty().Length(3).Matches("^[A-Za-z]{3}$");
    }
}

public sealed class RemoveFavoriteCurrencyCommandHandler : IRequestHandler<RemoveFavoriteCurrencyCommand, Result<bool>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IFavoritesRepository _favorites;
    private readonly IUnitOfWorkFactory _unitOfWork;

    public RemoveFavoriteCurrencyCommandHandler(
        ICurrentUser currentUser,
        IFavoritesRepository favorites,
        IUnitOfWorkFactory unitOfWork)
    {
        _currentUser = currentUser;
        _favorites = favorites;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<bool>> Handle(RemoveFavoriteCurrencyCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return Result<bool>.Failure(Error.Unauthorized("not_authenticated", "Authentication is required."));
        }

        var normalized = request.Code.Trim().ToUpperInvariant();
        if (normalized.Length != 3)
        {
            return Result<bool>.Failure(Error.Validation("invalid_currency_code", "Currency code must be 3 letters."));
        }

        var removed = await _favorites.RemoveAsync(_currentUser.UserId.Value, normalized, cancellationToken);
        if (removed)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result<bool>.Ok(removed);
    }
}