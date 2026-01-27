FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY task_corteos.sln ./
COPY src/CbrRatesLoader/CbrRatesLoader.csproj src/CbrRatesLoader/
RUN dotnet restore src/CbrRatesLoader/CbrRatesLoader.csproj

COPY src/CbrRatesLoader/ src/CbrRatesLoader/
RUN dotnet publish src/CbrRatesLoader/CbrRatesLoader.csproj -c Release -o /app/publish --no-restore


FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# TimeZoneInfo для IANA зон (например, Europe/Moscow)
RUN apt-get update \
  && apt-get install -y --no-install-recommends tzdata ca-certificates \
  && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish/ ./

ENV DOTNET_EnableDiagnostics=0

ENTRYPOINT ["dotnet", "CbrRatesLoader.dll"]

