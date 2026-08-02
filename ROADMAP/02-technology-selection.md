# 2. Technology Selection

## Objective

Lock the full package manifest with versions so builds are reproducible and no dependency arrives unreviewed.

## Scope

NuGet package selection and version pinning; no code.

## Architectural Decisions

- Central package management via `Directory.Packages.props` — one place to audit versions.
- Only packages listed here may be added; new dependencies require an ADR.

## Technology Decisions Requiring Approval

✅ **None outstanding.** P5 (caching) and P15 (load testing) resolved 2026-07-22:

| # | Decision | Recorded in |
|---|---|---|
| P5 | **`HybridCache`** with local L1; Azure deployments now add Redis L2 after scale-out became a v1 requirement | `ADR-0016`, superseded in part by `ADR-0029` |
| P15 | **k6**; scripts live outside the .NET solution | `ADR-0017` |

## Tasks

- [x] Create `Directory.Packages.props` with pinned versions of the package set below.
- [x] Create `Directory.Build.props`: `TreatWarningsAsErrors`, `GenerateDocumentationFile`, nullable enforced solution-wide.
- [x] Document each package's purpose in `Documentation/Decisions/ADR-0013-package-manifest.md`.

### Pinned versions (2026-07-22)

Central Package Management is enabled, so `PackageReference` entries carry no `Version` attribute — a `Version` attribute is now a build error (`NU1008`). Transitive pinning is on, which is how the `Microsoft.OpenApi` advisory below is fixed.

| Package | Version | | Package | Version |
|---|---|---|---|---|
| `Microsoft.AspNetCore.OpenApi` | 10.0.10 | | `Serilog.AspNetCore` | 10.0.0 |
| `Scalar.AspNetCore` | 2.16.16 | | `Microsoft.Extensions.Caching.Hybrid` | 10.8.0 |
| `Asp.Versioning.Mvc` (+ `.ApiExplorer`) | 10.0.0 | | `OpenTelemetry.*` (5 packages) | 1.17.0 |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | | `AspNetCore.HealthChecks.NpgSql` | 9.0.0 |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.10 | | `xunit.v3` | 3.2.2 |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.10 | | `xunit.runner.visualstudio` | 3.1.5 |
| `Microsoft.AspNetCore.Authentication.Google` | 10.0.10 | | `Microsoft.NET.Test.Sdk` | 18.8.1 |
| `AspNet.Security.OAuth.GitHub` | 10.0.0 | | `Microsoft.AspNetCore.Mvc.Testing` | 10.0.10 |
| `Isopoh.Cryptography.Argon2` | 2.0.0 | | `Testcontainers.PostgreSql` | 4.13.0 |
| `Otp.NET` | 1.4.1 | | `Microsoft.Extensions.TimeProvider.Testing` | 10.8.0 |
| `Fido2.AspNet` | 4.0.1 | | `Respawn` | 7.0.0 |
| `FluentValidation` (+ DI extensions) | 12.1.1 | | `NSubstitute` | 6.0.0 |
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | 10.0.10 | | `Testcontainers.Redis` | 4.13.0 |
| `Azure.Extensions.AspNetCore.DataProtection.Keys` | 1.6.3 | | `coverlet.msbuild` | 10.0.1 |
| `Azure.Identity` | 1.21.0 | | `Azure.Monitor.OpenTelemetry.Exporter` | 1.8.3 |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | 10.0.10 | | `Microsoft.Azure.StackExchangeRedis` | 3.2.0 |

**Security pin:** `Microsoft.OpenApi` 2.11.0 — `Microsoft.AspNetCore.OpenApi` 10.0.10 pulls 2.0.0 transitively, which is vulnerable to **GHSA-v5pm-xwqc-g5wc** (high; vulnerable ≤ 2.7.4, patched 2.7.5). Remove the pin when the parent package references ≥ 2.7.5 itself.

**Deviations from the package set below:** `NSubstitute` 6.0.0 was added (§20 needs test doubles; approved with P5/P15). `AspNetCore.HealthChecks.NpgSql` has no 10.x release and stays on 9.0.0. `xunit.v3` pins to the 3.2.2 stable line — its 4.0.0 line is prerelease only.

Package set (versions pinned at implementation time to latest stable for .NET 10):

| Package | Purpose |
|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | EF Core provider |
| `Microsoft.EntityFrameworkCore.Design` | migrations tooling |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | access-token validation |
| `FluentValidation` (+ DI extensions) | request validation (note: the deprecated `FluentValidation.AspNetCore` auto-validation package is **not** used; validation runs through our own filter, §10) |
| `Serilog.AspNetCore` | logging |
| `Scalar.AspNetCore` | interactive API docs |
| `Asp.Versioning.Mvc` + `Asp.Versioning.Mvc.ApiExplorer` | API versioning (P2) |
| `Isopoh.Cryptography.Argon2` | password hashing |
| `Otp.NET` | TOTP generation/validation |
| `Fido2.AspNet` (Fido2NetLib) | WebAuthn/passkeys |
| `AspNet.Security.OAuth.GitHub`, `Microsoft.AspNetCore.Authentication.Google` | social providers (P12) |
| `OpenTelemetry.Extensions.Hosting` + instrumentation packages | telemetry (§28) |
| `AspNetCore.HealthChecks.NpgSql` | readiness checks |
| `Microsoft.Extensions.Caching.Hybrid` | caching (P5) |
| Test-only: `xunit.v3`, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.PostgreSql`, `Microsoft.Extensions.TimeProvider.Testing`, `Respawn` | testing stack (§20–§22) |

## Expected Deliverables

`Directory.Packages.props`, `Directory.Build.props`, ADR-0013.

## Dependencies

§1 (P3/P4 affect file placement).

## Security Considerations

Pinned versions + CI vulnerability audit (§26) are the supply-chain defense; no floating versions.

**Implemented stronger than specified:** the audit does not wait for CI. `Directory.Build.props` sets `NuGetAudit=true`, `NuGetAuditMode=all` (transitive included), `NuGetAuditLevel=low`; combined with `TreatWarningsAsErrors`, **a newly published advisory against any dependency fails the build locally and in CI**. The correct response is to pin the patched version in `Directory.Packages.props`, not to suppress the warning.

This caught a real issue on first run: the template's `Microsoft.AspNetCore.OpenApi` 10.0.10 pulls `Microsoft.OpenApi` 2.0.0, which carries GHSA-v5pm-xwqc-g5wc (high). Transitive pinning to 2.11.0 cleared it.

Licence audit result: every pinned direct dependency is permissively licensed (MIT,
Apache-2.0, BSD-3-Clause, PostgreSQL or CC0-1.0). No commercial or copyleft dependency is
approved. Two packages needed resolving because their NuGet metadata uses the deprecated
`licenseUrl` field — `Isopoh.Cryptography.Argon2` (CC0-1.0) and `Otp.NET` (MIT); both are
recorded in ADR-0013.

## Testing Requirements

`dotnet build` succeeds with warnings-as-errors after manifest lands.

✅ Verified 2026-07-22: clean restore + build, **0 warnings, 0 errors**, `Microsoft.OpenApi` resolving to 2.11.0 in `project.assets.json`.

## Documentation Requirements

ADR-0013 lists every package with purpose and license note (AutoMapper-style license surprises are why).

## Definition of Done

Solution restores and builds clean from the pinned manifest; owner has approved P5 and P15.

- [x] Restores and builds clean from the pinned manifest (0 warnings, 0 errors, warnings-as-errors active).
- [x] Owner approved P5 (`HybridCache` in-memory) and P15 (k6).
- [x] `ADR-0013` lists every package with purpose and licence.

**✅ Definition of Done met 2026-07-22.**

## Questions for the Project Owner

1. ~~Approve `HybridCache` in-memory as the v1 cache (P5)?~~ ✅ **Yes** — `ADR-0016`.
2. ~~Approve k6 as the load-testing tool (P15)?~~ ✅ **Yes** — `ADR-0017`.
3. ~~`NSubstitute` added beyond the package set above (§20 needs test doubles)?~~ ✅ **Approved**; no assertion library added.

None outstanding.
