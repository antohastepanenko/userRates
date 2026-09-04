# syntax=docker/dockerfile:1.6
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore "src/Services/Migration/Rates.MigrationService.Host/Rates.MigrationService.Host.csproj"
RUN dotnet publish "src/Services/Migration/Rates.MigrationService.Host/Rates.MigrationService.Host.csproj" \
    --no-restore \
    --configuration Release \
    --output /app/publish \
    --framework net8.0

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV DOTNET_RUNNING_IN_CONTAINER=true

ENTRYPOINT ["dotnet", "Rates.MigrationService.Host.dll"]