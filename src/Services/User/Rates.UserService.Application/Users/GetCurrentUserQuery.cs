using MediatR;
using Rates.BuildingBlocks.Application;
using Rates.BuildingBlocks.Domain;

namespace Rates.UserService.Application.Users;

public sealed record CurrentUserView(Guid Id, string Name, DateTimeOffset CreatedAt);

public sealed record GetCurrentUserQuery : IQuery<Result<CurrentUserView>>;

public sealed class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserView>>
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserRepository _users;

    public GetCurrentUserQueryHandler(ICurrentUser currentUser, IUserRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<Result<CurrentUserView>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return Result<CurrentUserView>.Failure(
                Error.Unauthorized("not_authenticated", "Authentication is required."));
        }

        var user = await _users.FindByIdAsync(_currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Result<CurrentUserView>.Failure(
                Error.NotFound("user_not_found", "User no longer exists."));
        }

        return Result<CurrentUserView>.Ok(new CurrentUserView(user.Id, user.Name, user.CreatedAt));
    }
}