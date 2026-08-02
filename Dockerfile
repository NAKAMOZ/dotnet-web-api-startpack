# syntax=docker/dockerfile:1.7

FROM node:24-alpine AS frontend
WORKDIR /src/playground-ui

RUN corepack enable
COPY playground-ui/package.json playground-ui/pnpm-lock.yaml playground-ui/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY playground-ui ./
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props dotnet-web-api-startpack.csproj ./
RUN dotnet restore dotnet-web-api-startpack.csproj

COPY . .
COPY --from=frontend /src/wwwroot/playground ./wwwroot/playground
RUN dotnet publish dotnet-web-api-startpack.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    /p:SkipPlaygroundBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080

USER $APP_UID

HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=5 \
  CMD ["curl", "--fail", "--silent", "--show-error", "http://127.0.0.1:8080/health/live"]

ENTRYPOINT ["dotnet", "dotnet-web-api-startpack.dll"]
