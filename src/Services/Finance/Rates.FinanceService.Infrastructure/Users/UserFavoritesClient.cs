using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Rates.BuildingBlocks.Contracts;
using Rates.BuildingBlocks.Domain;
using Rates.BuildingBlocks.Infrastructure.Options;
using Rates.BuildingBlocks.Infrastructure.Security;
using Rates.FinanceService.Application.Users;

namespace Rates.FinanceService.Infrastructure.Users;

/// <summary>
/// HTTP-адаптер, используемый FinanceService для чтения кодов избранных валют пользователя
/// из внутреннего эндпоинта UserService. Этот эндпоинт никогда не проксируется через шлюз.
/// </summary>
public sealed class UserFavoritesClient : IUserFavoritesClient
{
    private readonly HttpClient _http;
    private readonly InternalServiceOptions _internalOptions;
    private readonly ILogger<UserFavoritesClient> _logger;

    public UserFavoritesClient(
        HttpClient http,
        IOptions<ServiceEndpointsOptions> endpoints,
        IOptions<InternalServiceOptions> internalOptions,
        ILogger<UserFavoritesClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _internalOptions = internalOptions.Value ?? throw new ArgumentNullException(nameof(internalOptions));
        _logger = logger;

        var baseUrl = endpoints.Value.UserServiceBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("ServiceEndpoints:UserServiceBaseUrl must be an absolute URI.");
        }

        _http.BaseAddress = new Uri(uri.ToString().TrimEnd('/') + "/", UriKind.Absolute);
    }

    public async Task<Result<IReadOnlyList<FavoriteEntry>>> GetFavoritesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Result<IReadOnlyList<FavoriteEntry>>.Failure(
                Error.Validation("invalid_user_id", "userId must be supplied."));
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"internal/v1/users/{userId:D}/favorite-codes");
        request.Headers.Add(InternalServiceHeaders.TokenHeaderName, _internalOptions.SharedToken);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<InternalFavoriteCodesResponse>(cancellationToken);
                if (payload is null)
                {
                    return Result<IReadOnlyList<FavoriteEntry>>.Failure(
                        Error.Unavailable("user_service_invalid_response", "UserService returned an empty response."));
                }

                var entries = payload.Items
                    .Select(item => new FavoriteEntry(item.Code, item.AddedAt))
                    .ToArray();
                return Result<IReadOnlyList<FavoriteEntry>>.Ok(entries);
            }

            var error = response.StatusCode switch
            {
                HttpStatusCode.NotFound => Error.NotFound("user_not_found", "User does not exist."),
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                    Error.Unavailable("user_service_unauthorized", "UserService rejected the internal request."),
                _ => Error.Unavailable("user_service_error", $"UserService returned {(int)response.StatusCode}."),
            };

            _logger.LogWarning(
                "UserService favorites request failed for {UserId}: {StatusCode}",
                userId,
                response.StatusCode);
            return Result<IReadOnlyList<FavoriteEntry>>.Failure(error);
        }
        catch (BrokenCircuitException ex)
        {
            _logger.LogWarning(ex, "UserService circuit is open while reading favorites for {UserId}", userId);
            return Result<IReadOnlyList<FavoriteEntry>>.Failure(
                Error.Unavailable("user_service_circuit_open", "UserService is temporarily unavailable."));
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "UserService is unreachable while reading favorites for {UserId}", userId);
            return Result<IReadOnlyList<FavoriteEntry>>.Failure(
                Error.Unavailable("user_service_unavailable", "UserService is temporarily unavailable."));
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("UserService timed out while reading favorites for {UserId}", userId);
            return Result<IReadOnlyList<FavoriteEntry>>.Failure(
                Error.Unavailable("user_service_timeout", "UserService request timed out."));
        }
    }
}
