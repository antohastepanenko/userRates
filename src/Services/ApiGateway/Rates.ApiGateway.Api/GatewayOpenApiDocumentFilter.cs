using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Rates.BuildingBlocks.Contracts;

namespace Rates.ApiGateway.Api;

/// <summary>
/// Описывает публичный контракт, принадлежащий шлюзу. Эндпоинты YARP не являются действиями
/// MVC, поэтому Swashbuckle не может обнаружить их автоматически; этот фильтр документирует
/// только публичные пути и никогда не раскрывает внутренние сервисные эндпоинты или DNS-имена
/// контейнеров.
/// </summary>
public sealed class GatewayOpenApiDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Info = new OpenApiInfo
        {
            Title = "Rates API Gateway",
            Version = "v1",
            Description = "Public Rates API routed through the API Gateway.",
        };
        swaggerDoc.Servers =
        [
            new OpenApiServer { Url = "/" },
        ];

        AddAuthPaths(swaggerDoc.Paths);
        AddUserPaths(swaggerDoc.Paths);
        AddFinancePaths(swaggerDoc.Paths);

        // Шлюз владеет внешним URL-пространством. Схемы регистрируются вручную, потому что
        // маршруты YARP не порождают записи ApiDescription для Swashbuckle.
        swaggerDoc.Components ??= new OpenApiComponents();
        swaggerDoc.Components.Schemas["TokenResponse"] = context.SchemaGenerator.GenerateSchema(typeof(TokenResponse), context.SchemaRepository);
        swaggerDoc.Components.Schemas["CurrentUserResponse"] = context.SchemaGenerator.GenerateSchema(typeof(CurrentUserResponse), context.SchemaRepository);
        swaggerDoc.Components.Schemas["CurrencyRatesResponse"] = context.SchemaGenerator.GenerateSchema(typeof(CurrencyRatesResponse), context.SchemaRepository);
        swaggerDoc.Components.Schemas["FavoriteCurrenciesResponse"] = context.SchemaGenerator.GenerateSchema(typeof(FavoriteCurrenciesResponse), context.SchemaRepository);
    }

    private static void AddAuthPaths(OpenApiPaths paths)
    {
        paths["/api/v1/auth/register"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Post] = CreateOperation("Register a user", requiresAuthentication: false, successStatus: "201"),
            },
        };
        paths["/api/v1/auth/login"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Post] = CreateOperation("Login", requiresAuthentication: false),
            },
        };
        paths["/api/v1/auth/refresh"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Post] = CreateOperation("Rotate refresh token", requiresAuthentication: false),
            },
        };
        paths["/api/v1/auth/logout"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Post] = CreateOperation("Logout", requiresAuthentication: true, successStatus: "204"),
            },
        };
    }

    private static void AddUserPaths(OpenApiPaths paths)
    {
        paths["/api/v1/users/me"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Get] = CreateOperation("Get the current user", requiresAuthentication: true),
            },
        };
        paths["/api/v1/users/me/favorites"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Get] = CreateOperation("List favorite currencies", requiresAuthentication: true),
            },
        };
        paths["/api/v1/users/me/favorites/{code}"] = new OpenApiPathItem
        {
            Parameters =
            [
                new OpenApiParameter
                {
                    Name = "code",
                    In = ParameterLocation.Path,
                    Required = true,
                    Schema = new OpenApiSchema { Type = "string", MinLength = 3, MaxLength = 3 },
                },
            ],
            Operations =
            {
                [OperationType.Put] = CreateOperation("Add a favorite currency", requiresAuthentication: true, successStatus: "204"),
                [OperationType.Delete] = CreateOperation("Remove a favorite currency", requiresAuthentication: true, successStatus: "204"),
            },
        };
    }

    private static void AddFinancePaths(OpenApiPaths paths)
    {
        paths["/api/v1/finance/currencies/me"] = new OpenApiPathItem
        {
            Operations =
            {
                [OperationType.Get] = CreateOperation("Get rates for favorite currencies", requiresAuthentication: true),
            },
        };
    }

    private static OpenApiOperation CreateOperation(
        string summary,
        bool requiresAuthentication,
        string successStatus = "200")
    {
        var responses = new OpenApiResponses
        {
            [successStatus] = new OpenApiResponse { Description = "Success" },
                ["400"] = new OpenApiResponse { Description = "Validation error" },
                ["401"] = new OpenApiResponse { Description = "Authentication required" },
                ["404"] = new OpenApiResponse { Description = "Resource not found" },
                ["409"] = new OpenApiResponse { Description = "Conflict" },
                ["429"] = new OpenApiResponse { Description = "Rate limit exceeded" },
            ["503"] = new OpenApiResponse { Description = "Dependency unavailable" },
        };

        var operation = new OpenApiOperation
        {
            Summary = summary,
            Responses = responses,
        };

        if (requiresAuthentication)
        {
            operation.Security =
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    }] = Array.Empty<string>(),
                },
            ];
        }

        return operation;
    }
}