# 3. Solution and Project Structure

## Objective

Turn the bare template into the mandated modular skeleton with a pure composition-root `Program.cs`, ready to receive features.

## Scope

Git init, solution file, directory tree, `Program.cs` skeleton, extension-method composition pattern, test project scaffolds. No feature code.

## Architectural Decisions

- `Program.cs` contains only: builder creation, calls to `Add*` extension methods, calls to `Use*`/`Map*` pipeline extension methods, `app.Run()`. Hard rule enforced in code review.
- Composition extension methods live in `Extensions/`, grouped by concern (one file each): `ServiceCollectionExtensions.Auth.cs`, `ServiceCollectionExtensions.Data.cs`, `ServiceCollectionExtensions.Validation.cs`, `ApplicationBuilderExtensions.Pipeline.cs`, etc.
- The weatherforecast sample endpoint and record are deleted — no minimal API handlers remain.
- Layout (P4): **revised by `ADR-0018`** — the API project stays at the repository root; tests under `tests/`. Root namespace is `Api`. The `src/Api/` layout in `ADR-0014` was implemented and then reversed by the owner on review.

## Technology Decisions Requiring Approval

✅ **None outstanding — all three resolved 2026-07-22.** P2: URL-segment `/api/v1/…` (`ADR-0015`). P3: all four directories approved (`ADR-0014`). P4: flat root layout, root namespace `Api` (`ADR-0018`, superseding `ADR-0014`). This workstream is unblocked; it depends only on §2 landing the package manifest first.

## Tasks

- [x] ~~`git init`~~ (repo already initialised); `.gitignore` already present (333 lines, already excludes `appsettings.*.local.json`, user secrets, `.env`); added `.gitattributes` and extended `.editorconfig`.
- [x] Create the solution; move project per P4; fix `RootNamespace`.
- [x] Create all directories from the target tree with `.gitkeep` where initially empty — 19 `.gitkeep` files.
- [x] Rewrite `Program.cs` as composition root calling stub extension methods in `Extensions/`.
- [x] Add `tests/UnitTests/UnitTests.csproj` and `tests/IntegrationTests/IntegrationTests.csproj` referencing the API project.
- [x] Update `CLAUDE.md`: new layout, new run commands, template description removed.
- [x] Delete the weatherforecast endpoint, record, and its `.http` sample; replaced with the per-feature `http/` layout (populated in §24).
- [ ] **Commit** — left to the project owner; the working tree is staged-ready and verified green.

### Deviations from this workstream's original text

Four, each with a reason:

1. **Solution file is `dotnet-web-api-startpack.slnx`, not `.sln`.** `dotnet new sln` on the .NET 10 SDK emits the XML `.slnx` format by default. It is cleaner (no GUID soup, merge-friendly) and fully supported by `dotnet build`/`test`/`restore`. Requires SDK 10, VS 2022 17.14+, or Rider 2025.1+. Reversible with `dotnet new sln --format sln` if older tooling matters.

2. **"One type per file via `IDE0040`-family severities" is not implementable as written.** `IDE0040` governs *accessibility modifiers*; the .NET SDK ships **no** analyzer for one-type-per-file. What was implemented instead: `IDE0161` (file-scoped namespaces) and `IDE0040` are both **errors** at build via `EnforceCodeStyleInBuild`, verified by deliberately compiling a violating file. One-type-per-file is a review rule until §20 adds an architecture test.

3. **`IDE0005` (unused usings) is scoped to application code only.** The rule only reports during build when `GenerateDocumentationFile` is true ([roslyn#41640](https://github.com/dotnet/roslyn/issues/41640)); test projects switch doc generation off, and leaving the rule on there makes the compiler emit an `EnableGenerateDocumentationFile` diagnostic that `TreatWarningsAsErrors` turns into a build failure.

4. **The `src/Api/` layout was implemented, then reversed.** P4 approved `src/` + `tests/` and this workstream built it that way. On reviewing the result the owner rejected the nesting, so the API project moved back to the repository root and the `Api` project name was dropped. Recorded in `ADR-0018`, which supersedes the layout half of `ADR-0014`; the directory *set* from P3 is unchanged. The mechanical consequence is that the csproj now needs `<DefaultItemExcludes>` for `tests/**` and the other non-source top-level directories — without it, test files compile into the API assembly.

### Also landed (not in the original list)

- `EnforceCodeStyleInBuild` added to `Directory.Build.props` — without it the `.editorconfig` severities apply only in the IDE, so the conventions this workstream mandates would not be enforced on the command line or in CI.
- `.gitignore` extended with `*.private.env.json` so HTTP-client credential files cannot be committed.
- A `CompositionRootTests` smoke test that boots the real host through `WebApplicationFactory`. The Definition of Done only asks for "empty test runs green", but an empty run proves the runner works, not the composition root. This proves every `Add*` extension resolves and the pipeline starts.

## Expected Deliverables

`.slnx`, root-level project skeleton with all directories, `tests/` scaffolds, `.editorconfig`, `.gitignore`, updated `CLAUDE.md`, first commit.

## Dependencies

§1 (P3/P4 answers), §2 (package manifest referenced by csproj).

## Security Considerations

`.gitignore` must exclude `appsettings.*.local.json`, user-secrets never enter the repo; verify no secrets in the initial commit.

✅ Verified: the existing `.gitignore` already covers `appsettings.*.local.json`, `secrets.json`, `.env`, `secrets/` and `.usersecrets/`. Extended with `*.private.env.json` for HTTP-client credential files. No secret material exists in the tree — the only configuration files are the template's `appsettings.json` / `appsettings.Development.json`, both of which contain logging levels and nothing else.

## Testing Requirements

`dotnet build` + empty test runs green in both test projects.

✅ `dotnet build`: **0 warnings, 0 errors** across all three projects. `dotnet test`: **2 passed, 0 failed** — `UnitTests` (10 ms) and `IntegrationTests` (219 ms).

## Documentation Requirements

`README.md` quickstart section (clone → compose up → run) stubbed, completed in §24.

✅ `README.md` created at the repo root with a stubbed quickstart (compose steps marked `TODO §24`, migrations `TODO §8`).

## Definition of Done

Solution builds; `Program.cs` under ~40 lines with zero business logic; all mandated directories exist; sample code gone; committed.

- [x] Solution builds — 0 warnings, 0 errors; both test projects green.
- [x] `Program.cs` is **16 lines**, zero business logic, calls only extension methods.
- [x] All mandated directories exist (19 with `.gitkeep`).
- [x] Sample code gone — no `weatherforecast` reference anywhere; no `app.MapGet`/`MapPost` handler remains; the served OpenAPI document is `"paths": {}`.
- [ ] **Committed** — left to the project owner.

**Everything except the commit is done and verified.**

## Questions for the Project Owner

1. ~~Approve URL-segment versioning `/api/v1/…` (P2)?~~ ✅ **Yes**, approved 2026-07-22 — `ADR-0015`.
2. ~~Preferred root namespace after the move?~~ ✅ **`Api`** — recorded in `ADR-0014`.

None outstanding.
