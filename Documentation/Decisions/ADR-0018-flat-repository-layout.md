# ADR-0018: Flat Repository Layout — API Project at the Root

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Supersedes:** the layout half of [ADR-0014](ADR-0014-solution-layout-and-directories.md) (pending decision **P4**). The directory-set half of ADR-0014 (**P3**) stands unchanged.
- **Affects:** §3 (skeleton), every path reference in the repository

## Context

[ADR-0014](ADR-0014-solution-layout-and-directories.md) approved a `src/Api/` + `tests/` layout, and §3 implemented it. On review of the result, the owner rejected the nesting: `src/Api/Api.csproj` puts two directory levels between the repository root and the code, and introduces a project named `Api` that duplicates what the repository already identifies.

The original argument for `src/` was keeping solution-level artefacts (`Dockerfile`, `docker-compose.yml`, `.github/`, `Documentation/`, `ROADMAP/`) separate from project source. That argument was accepted at decision time and is now outweighed by the cost of the indirection in day-to-day work.

This is a reversal of an approved decision, made with the previous reasoning in view.

## Decision

**The API project lives at the repository root.** `dotnet-web-api-startpack.csproj` sits beside `Program.cs`, and every application directory — `Controllers/`, `DTOs/`, `Services/`, `Models/`, `Data/`, `Extensions/`, and the rest — is a direct child of the root. There is no `src/` and no project named `Api`.

**Test projects stay under `tests/`** as separate projects. They cannot be folded into the API project: a test project needs its own SDK settings (`OutputType=Exe`, `IsTestProject`), its own package set, and must not ship inside the API assembly.

**The root project excludes non-source directories explicitly.** This is the mechanical consequence that makes the flat layout safe, and it is not optional:

```xml
<DefaultItemExcludes>
  $(DefaultItemExcludes);
  tests/**; Documentation/**; ROADMAP/**; http/**; .git/**
</DefaultItemExcludes>
```

**`RootNamespace` remains `Api`**, so code namespaces stay `Api.Extensions`, `Api.Controllers` and so on. `AssemblyName` is left to default from the project filename rather than being set to `Api`.

## Alternatives considered

**Keeping `src/Api/` + `tests/`** — the superseded ADR-0014 decision. Rejected by the owner on ergonomics: the nesting is friction on every path, and the `Api` project name adds a naming layer the repository does not need.

**Flat, with tests also at the root** (`UnitTests/`, `IntegrationTests/` as siblings of `Controllers/`). Rejected: it puts test projects inside the API project's glob scope, so every test file would need excluding individually, and it mixes application directories with test projects at the same level — a worse version of the nesting problem it would be solving.

**Flat, with tests inside the API project** (no separate test projects). Not viable — `xunit.v3` requires `OutputType=Exe` and test dependencies would ship in the API assembly.

**Renaming the root namespace away from `Api`.** Considered and rejected as churn: it would touch every file for no behavioural gain, and `Api.*` reads correctly regardless of where the project file sits.

## Consequences

- Run commands return to the simple form: `dotnet run` from the root, `dotnet watch run`. No `--project` argument.
- **`DefaultItemExcludes` is now load-bearing.** The SDK globs `**/*.cs` from the project directory, which is the repository root — so without the exclusion, `tests/**/*.cs` compiles into the API assembly and every test type is defined twice. Any new top-level directory holding C# that is not application source must be added to that list.
- The root directory mixes application directories with repository-level artefacts. That is the cost the `src/` layout was avoiding, accepted deliberately.
- Every path in the roadmap, `CLAUDE.md`, `README.md`, and ADR-0014's tree diagram that referenced `src/Api/` is updated. ADR-0014 itself is **not** edited beyond its status line — the superseded decision stays readable as written, per the numbering rules in [`README.md`](README.md).
- `Directory.Packages.props` and `Directory.Build.props` stay at the root and still govern the API and both test projects. Their placement was never dependent on the `src/` layout.
- `.gitkeep` files remain in the empty application directories; git does not track directories.
