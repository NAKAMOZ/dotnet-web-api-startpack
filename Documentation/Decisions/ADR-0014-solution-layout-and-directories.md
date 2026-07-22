# ADR-0014: Solution Layout and Directory Structure

- **Status:** **Partially superseded by [ADR-0018](ADR-0018-flat-repository-layout.md)** (2026-07-22)
  - The **P4 layout decision below (`src/Api/` + `tests/`) is no longer in force** — the API project lives at the repository root. Read ADR-0018 for the current layout.
  - The **P3 decision (the four additional directories) stands unchanged** and is still authoritative.
  - The text below is preserved as written, per the numbering rules in [`README.md`](README.md). It is not edited to match the new decision.
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** **Resolves pending decisions P3 and P4** (`ROADMAP/00-overview.md`)
- **Affects:** §2 (props file placement), §3 (skeleton), and the physical location of every file thereafter

## Context

The repository currently holds the `dotnet new webapi` template flat at the root: `.csproj`, `Program.cs`, `appsettings.json`, and `Properties/` all sit beside `ROADMAP/` and `CLAUDE.md`. There is no solution file.

Two questions had to be answered before §3 could create anything, because both determine where every subsequent file lands:

- **P4** — does the project stay flat at the root, or move into `src/` with tests under `tests/`?
- **P3** — the roadmap mandates a set of directories, but four more were proposed. Are they approved?

Answering these after §3 would mean moving files that already exist.

## Decision

**P4 — `src/` and `tests/`.** The API project moves to `src/Api/`; test projects live at `tests/UnitTests/` and `tests/IntegrationTests/`. A `dotnet-web-api-startpack.sln` at the root ties them together.

**P3 — all four proposed directories are approved**, each for a reason that follows from an already-approved decision rather than from preference:

| Directory | Why it must exist |
|---|---|
| `Validators/` | FluentValidation is approved and one validator per request DTO is mandated ([ADR-0009](ADR-0009-validation-and-mapping.md)). Nesting them inside `DTOs/` would put two responsibilities in one tree; a sibling directory mirroring `DTOs/<Feature>/` keeps both discoverable. |
| `Extensions/` | `Program.cs` may only call extension methods ([ADR-0007](ADR-0007-runtime-and-api-style.md)). `AddApiServices`, `AddAuthenticationSetup`, `UseApiPipeline` need a home, and `Configuration/` is reserved for typed options classes. |
| `Exceptions/` | Services signal failure with typed domain exceptions (`EmailAlreadyRegisteredException`, `TokenReuseDetectedException`) translated centrally to Problem Details. One type per file requires a directory. |
| `BackgroundServices/` | Expired session and token cleanup (P9) is neither a controller-called service nor middleware. Hosted workers are a third category. |

The full target tree is in `ROADMAP/00-overview.md`. **Root namespace becomes `Api`** after the move, replacing `dotnet_web_api_startpack`.

## Alternatives considered

**Flat root layout (P4).** Less churn — nothing moves. Rejected because the root is not only the project's: `docker-compose.yml`, `Dockerfile`, `.github/workflows/`, `Documentation/`, `ROADMAP/`, and the `.sln` all live there. Mixing solution-level artefacts with project source makes the root a directory where nothing has an obvious place. `src/` + `tests/` is also the convention a .NET developer expects, which is worth something on its own.

**Validators inside `DTOs/`** (P3). Co-locates a DTO with its validator, which has a real argument in its favour. Rejected because it merges the *shape* of a request with the *rules* about it, and doubles the file count in a tree that is already per-feature.

**Validation attributes instead of a `Validators/` directory.** Already rejected in [ADR-0009](ADR-0009-validation-and-mapping.md) on expressiveness grounds; the directory question does not reopen it.

**Registrations inline in `Program.cs`, no `Extensions/`.** Directly contradicts the composition-root rule. Not viable.

**Generic exceptions instead of `Exceptions/`** — throwing `InvalidOperationException` with a message. Rejected: central translation to Problem Details needs to map failure *kinds* to status codes, and a message string is not a kind.

**`Hangfire`/`Quartz` instead of `BackgroundServices/`.** Rejected under P9 for v1; plain `BackgroundService` needs no scheduler infrastructure for two cleanup jobs.

## Consequences

- §3 performs the move: relocating the `.csproj`, updating `RootNamespace`, creating the solution, and creating every directory with `.gitkeep` where initially empty.
- **Every path in `CLAUDE.md` and the roadmap referring to the flat layout becomes stale** once §3 runs. §3's task list already includes updating `CLAUDE.md`; this ADR is the reason.
- Run commands change: `dotnet run` from the root no longer finds a project, becoming `dotnet run --project src/Api`.
- `Directory.Packages.props` and `Directory.Build.props` (§2) go at the **repository root** so they apply to `src/` and `tests/` alike — central package management only works if it sits above every project it governs.
- Namespaces change from `dotnet_web_api_startpack` to `Api`. Cheap now, expensive later; doing it before feature code exists is why the decision is made here rather than deferred.
- Eight directories mandated by the roadmap plus these four is a lot of empty structure at first. Accepted deliberately: the alternative is deciding placement 40 times under time pressure, which is how files end up in `Helpers/`.
