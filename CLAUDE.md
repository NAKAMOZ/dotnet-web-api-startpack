# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

This is a freshly scaffolded ASP.NET Core minimal API (`dotnet new webapi`), targeting .NET 10. It currently contains only the default template code (the `/weatherforecast` sample endpoint) and has not yet been customized. Treat this as a starting point, not an established codebase with conventions to preserve — when adding features, establish sensible structure rather than assuming existing patterns are load-bearing.

The project root is `dotnet-web-api-startpack/`, which contains the `.csproj` directly (no `.sln` file exists yet).

## Commands

Run all commands from the `dotnet-web-api-startpack/` directory (where the `.csproj` lives).

- **Run the API**: `dotnet run` — starts on `http://localhost:5035` (see `Properties/launchSettings.json` for the `http` profile; an `https` profile is also available on port 7052).
- **Build**: `dotnet build`
- **Restore packages**: `dotnet restore`
- **Watch mode** (auto-reload on changes): `dotnet watch run`

There are no tests in the project yet, and no test project has been created.

To exercise the sample endpoint, use the included `dotnet-web-api-startpack.http` file (works with the VS Code REST Client / Rider / Visual Studio HTTP client), or:
```
curl http://localhost:5035/weatherforecast
```

## Architecture

- **`Program.cs`** — single-file minimal API entry point. Services, middleware, and endpoints are all registered here via top-level statements (no `Startup.cs` split). New endpoints are currently defined inline with `app.MapGet`/`MapPost`/etc.
- **OpenAPI**: `builder.Services.AddOpenApi()` + `app.MapOpenApi()` registers the built-in ASP.NET Core OpenAPI document generator (Microsoft.AspNetCore.OpenApi package), gated behind `app.Environment.IsDevelopment()`. There is no Swagger UI wired up — only the raw OpenAPI JSON document is exposed.
- **Configuration**: standard ASP.NET Core layered config — `appsettings.json` (base) + `appsettings.Development.json` (environment overrides), selected via `ASPNETCORE_ENVIRONMENT` (set to `Development` in both launch profiles).
- **Target framework**: `net10.0`, with `Nullable` and `ImplicitUsings` enabled — write nullable-aware code and rely on implicit global usings rather than adding explicit `using` statements for common BCL namespaces.
