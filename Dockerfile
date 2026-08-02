# syntax=docker/dockerfile:1.7@sha256:a57df69d0ea827fb7266491f2813635de6f17269be881f696fbfdf2d83dda33e

FROM node:24-alpine@sha256:f70403e87646dc51b45295f4b8b70cdad0b63d2297c4c9899119b03f7af7a6b3 AS frontend
WORKDIR /src/playground-ui

RUN corepack enable
COPY playground-ui/package.json playground-ui/pnpm-lock.yaml playground-ui/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile

COPY playground-ui ./
RUN pnpm build

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:72dd743782f2ae7e5476fd64f6a460045e3998dc862218b80e6944cba79a01b0 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:f1126d438ccc359f51cc6d4701a8deae513856cf10f5fe645d29ea6403dcac6b AS runtime
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
