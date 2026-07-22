# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project state

The code is still the default `dotnet new webapi` template (.NET 10, single `/weatherforecast` minimal-API endpoint in `Program.cs`) — but the project's future is fully specified in `ROADMAP/`: a Better Auth–inspired authentication & authorization REST API. Planning is complete; **implementation has not started and must not start until the project owner explicitly requests it** (see `ROADMAP/README.md`).

Do not invent structure or conventions from the template code. `Documentation/Decisions/` is the source of truth for architectural decisions; the roadmap is the source of truth for workstream scope and sequencing. Where the two disagree, the ADR wins — `ROADMAP/` is a planning artifact that §29 archives at v1 close.

The project root is `dotnet-web-api-startpack/`, which contains the `.csproj` directly (no `.sln` file yet — creating one is a roadmap task, §3).

## Commands

Run all commands from the repo root (where the `.csproj` lives).

- **Run the API**: `dotnet run` — starts on `http://localhost:5035` (`http` profile in `Properties/launchSettings.json`; `https` profile on port 7052).
- **Build**: `dotnet build`
- **Watch mode**: `dotnet watch run`

No tests exist yet (test projects are roadmap workstreams §20–§23). To exercise the sample endpoint, use `dotnet-web-api-startpack.http` or `curl http://localhost:5035/weatherforecast`.

## Decision record (`Documentation/`)

Written by workstream §1 and durable beyond the roadmap:

- `Documentation/Decisions/README.md` — ADR index and numbering rules. **`ADR-0013` is reserved for §2** (package manifest) and is intentionally missing; don't fill the gap.
- `Documentation/Decisions/ADR-0001`–`ADR-0012` — the approved-decisions table, one page each. `ADR-0014` (layout/directories, P3+P4) and `ADR-0015` (versioning, P2).
- `Documentation/Scope.md` — v1 in-scope capabilities and the deferred list with reasons.

New decisions get a new ADR. Superseding one sets the old file's status to `Superseded by ADR-XXXX` rather than editing or deleting it.

## The roadmap (`ROADMAP/`)

- `ROADMAP/README.md` — status board: per-workstream status, phase ordering, what happens next.
- `ROADMAP/00-overview.md` — the anchor document: approved decisions, pending decisions (P5–P18 open), target directory structure, 13-entity model, full endpoint inventory.
- `ROADMAP/01–29-*.md` — one file per workstream, each ending with a **Definition of Done**.

Rules encoded in the roadmap that override any default instinct:

- **Approved decisions are final** (table in `00-overview.md`): attribute-routed MVC **controllers only — no minimal API endpoints**; `Program.cs` strictly a composition root; RFC 9457 Problem Details for all errors; PostgreSQL + EF Core; FluentValidation; manual mapping extensions (no AutoMapper); Serilog; Argon2id password hashing; ES256 JWTs (15-min TTL) + opaque rotating refresh tokens; xUnit + Testcontainers; Scalar for API docs.
- **Pending decisions P5–P18** carry recommendations but require owner approval. Do not start a workstream whose dependencies or blocking pending decisions are unresolved (e.g., P5/P15 block §2; P12/P13/P17 block §4). **P1–P4 are resolved** (2026-07-22): 7-day absolute session cap, URL-segment `/api/v1/…` versioning, all four proposed directories approved, `src/Api/` + `tests/` layout with root namespace `Api`.
- **Status discipline**: mark a workstream ✅ in `README.md` only when its Definition of Done is met; 🔄 for partial progress; fine-grained checkboxes live inside each workstream file.
- **Same-PR rule**: each feature slice lands its controller, DTOs, validators, services, tests, and per-endpoint Markdown doc (`Documentation/`) in one PR to prevent doc drift.
- The template sample code (`/weatherforecast`) is slated for deletion in Phase A — don't build on it.

## Current code (template, pre-implementation)

- **`Program.cs`** — minimal-API entry point with the sample endpoint. Will become a pure composition root calling extension methods once implementation starts.
- **OpenAPI**: `AddOpenApi()` + `MapOpenApi()` (built-in generator, Development only). No UI yet — Scalar comes with §18.
- **Configuration**: standard layered `appsettings.json` + `appsettings.Development.json`, `ASPNETCORE_ENVIRONMENT=Development` in both launch profiles.
- **Target framework**: `net10.0` with `Nullable` and `ImplicitUsings` enabled — write nullable-aware code and rely on implicit usings. Root namespace is `dotnet_web_api_startpack`.
