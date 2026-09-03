using Microsoft.EntityFrameworkCore;
using Rates.UserService.Application;
using Rates.UserService.Domain;

namespace Rates.UserService.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _db;

    public UserRepository(IdentityDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> FindByNameAsync(string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Task.FromResult<User?>(null);
        }

        return _db.Users.FirstOrDefaultAsync(u => u.Name == name, cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        await _db.Users.AddAsync(user, cancellationToken);
    }
}

public sealed class EfRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IdentityDbContext _db;

    public EfRefreshTokenRepository(IdentityDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        await _db.RefreshTokens.AddAsync(token, cancellationToken);
    }

    public Task<RefreshToken?> FindActiveByPlainAsync(string plainToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(plainToken))
        {
            return Task.FromResult<RefreshToken?>(null);
        }

        var hash = Rates.UserService.Infrastructure.Auth.RefreshTokenService.Hash(plainToken);
        return _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null && t.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task RevokeAsync(Guid tokenId, DateTimeOffset now, Guid? replacedById, CancellationToken cancellationToken)
    {
        var token = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);
        token?.Revoke(now, replacedById);
    }
}