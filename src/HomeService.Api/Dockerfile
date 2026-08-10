FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/HomeService.Api/HomeService.Api.csproj
RUN dotnet publish src/HomeService.Api/HomeService.Api.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
ENV STORAGE_ROOT_PATH=/app/storage
RUN mkdir -p /app/storage
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HomeService.Api.dll"]
