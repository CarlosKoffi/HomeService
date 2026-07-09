FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG PROJECT=src/HomeService.Api/HomeService.Api.csproj
WORKDIR /src
COPY . .
RUN dotnet restore $PROJECT
RUN dotnet publish $PROJECT -c Release -o /app/publish --no-restore

FROM base AS final
ARG APP_DLL=HomeService.Api.dll
ENV APP_DLL=$APP_DLL
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["sh", "-c", "dotnet $APP_DLL"]
