# syntax=docker/dockerfile:1.6
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore "src/Services/ApiGateway/Rates.ApiGateway.Api/Rates.ApiGateway.Api.csproj"
RUN dotnet publish "src/Services/ApiGateway/Rates.ApiGateway.Api/Rates.ApiGateway.Api.csproj" \
    --no-restore \
    --configuration Release \
    --output /app/publish \
    --framework net8.0

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

EXPOSE 8080
ENTRYPOINT ["dotnet", "Rates.ApiGateway.Api.dll"]