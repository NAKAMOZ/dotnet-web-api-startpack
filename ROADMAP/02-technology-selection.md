# 2. Technology Selection

## Objective

Lock the full package manifest with versions so builds are reproducible and no dependency arrives unreviewed.

## Scope

NuGet package selection and version pinning; no code.

## Architectural Decisions

- Central package management via `Directory.Packages.props` — one place to audit versions.
- Only packages listed here may be added; new dependencies require an ADR.

## Technology Decisions Requiring Approval

P5 (caching), P15 (k6). All other stack items already approved.

## Tasks

- [ ] Create `Directory.Packages.props` with pinned versions of the package set below.
- [ ] Create `Directory.Build.props`: `TreatWarningsAsErrors`, `GenerateDocumentationFile`, nullable enforced solution-wide.
- [ ] Document each package's purpose in `Documentation/Decisions/ADR-0013-package-manifest.md`.

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

## Testing Requirements

`dotnet build` succeeds with warnings-as-errors after manifest lands.

## Documentation Requirements

ADR-0013 lists every package with purpose and license note (AutoMapper-style license surprises are why).

## Definition of Done

Solution restores and builds clean from the pinned manifest; owner has approved P5 and P15.

## Questions for the Project Owner

1. Approve `HybridCache` in-memory as the v1 cache (P5)?
2. Approve k6 as the load-testing tool (P15)?
