# 3. Solution and Project Structure

## Objective

Turn the bare template into the mandated modular skeleton with a pure composition-root `Program.cs`, ready to receive features.

## Scope

Git init, solution file, directory tree, `Program.cs` skeleton, extension-method composition pattern, test project scaffolds. No feature code.

## Architectural Decisions

- `Program.cs` contains only: builder creation, calls to `Add*` extension methods, calls to `Use*`/`Map*` pipeline extension methods, `app.Run()`. Hard rule enforced in code review.
- Composition extension methods live in `Extensions/`, grouped by concern (one file each): `ServiceCollectionExtensions.Auth.cs`, `ServiceCollectionExtensions.Data.cs`, `ServiceCollectionExtensions.Validation.cs`, `ApplicationBuilderExtensions.Pipeline.cs`, etc.
- The weatherforecast sample endpoint and record are deleted — no minimal API handlers remain.
- Layout (P4, **approved** — `ADR-0014`): project moves to `src/Api/`, tests under `tests/`. Root namespace becomes `Api`.

## Technology Decisions Requiring Approval

✅ **None outstanding — all three resolved 2026-07-22.** P2: URL-segment `/api/v1/…` (`ADR-0015`). P3: all four directories approved (`ADR-0014`). P4: `src/Api/` + `tests/`, root namespace `Api` (`ADR-0014`). This workstream is unblocked; it depends only on §2 landing the package manifest first.

## Tasks

- [ ] `git init`; add `.gitignore` (dotnet template), `.gitattributes`, `.editorconfig` (dotnet conventions, file-scoped namespaces, one type per file rule via `IDE0040`-family severities).
- [ ] Create `dotnet-web-api-startpack.sln`; move project per P4; fix `RootNamespace`.
- [ ] Create all directories from the target tree with `.gitkeep` where initially empty (`wwwroot/`, `Attributes/`).
- [ ] Rewrite `Program.cs` as composition root calling stub extension methods in `Extensions/`.
- [ ] Add `tests/UnitTests/UnitTests.csproj` and `tests/IntegrationTests/IntegrationTests.csproj` referencing the API project.
- [ ] Update `CLAUDE.md`: new layout, new run commands, note that the flat-template description is obsolete.
- [ ] Delete the weatherforecast endpoint, record, and its `.http` sample; replace `dotnet-web-api-startpack.http` with a per-feature `.http` layout placeholder (populated in §24).
- [ ] Initial commit.

## Expected Deliverables

`.sln`, `src/Api/` skeleton with all directories, `tests/` scaffolds, `.editorconfig`, `.gitignore`, updated `CLAUDE.md`, first commit.

## Dependencies

§1 (P3/P4 answers), §2 (package manifest referenced by csproj).

## Security Considerations

`.gitignore` must exclude `appsettings.*.local.json`, user-secrets never enter the repo; verify no secrets in the initial commit.

## Testing Requirements

`dotnet build` + empty test runs green in both test projects.

## Documentation Requirements

`README.md` quickstart section (clone → compose up → run) stubbed, completed in §24.

## Definition of Done

Solution builds; `Program.cs` under ~40 lines with zero business logic; all mandated directories exist; sample code gone; committed.

## Questions for the Project Owner

1. ~~Approve URL-segment versioning `/api/v1/…` (P2)?~~ ✅ **Yes**, approved 2026-07-22 — `ADR-0015`.
2. ~~Preferred root namespace after the move?~~ ✅ **`Api`** — recorded in `ADR-0014`.

None outstanding.
