# syntax=docker/dockerfile:1.6
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet restore "src/Services/RatesWorker/Rates.RatesWorker.Host/Rates.RatesWorker.Host.csproj"
RUN dotnet publish "src/Services/RatesWorker/Rates.RatesWorker.Host/Rates.RatesWorker.Host.csproj" \
    --no-restore \
    --configuration Release \
    --output /app/publish \
    --framework net8.0

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true

ENTRYPOINT ["dotnet", "Rates.RatesWorker.Host.dll"]