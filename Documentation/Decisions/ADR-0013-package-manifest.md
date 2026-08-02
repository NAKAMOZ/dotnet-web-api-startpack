# ADR-0013: Package Manifest and Central Package Management

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** Workstream §2 (`ROADMAP/02-technology-selection.md`)
- **Affects:** every project in the solution; §3 (skeleton), §26 (CI audit), §29 (dependency cadence)

## Context

Dependencies are the part of the system nobody wrote and everybody ships. For an authentication service they are also part of the attack surface: a compromised or vulnerable package in the password-hashing or token path is a compromise of the product.

Two failure modes to design against:

1. **Version drift.** Floating versions (`10.*`) or per-project version attributes mean two projects can resolve different versions of the same package, and a restore months later can produce a different build from the same commit.
2. **Unreviewed arrival.** A package added in passing during feature work is a dependency nobody evaluated — the AutoMapper licence change ([ADR-0009](ADR-0009-validation-and-mapping.md)) is the recorded example of why that matters.

## Decision

**Central Package Management.** All versions are declared in `Directory.Packages.props` at the repository root, with `ManagePackageVersionsCentrally=true`. Project files reference packages **without** a `Version` attribute. Exact pins only — no wildcards, no ranges.

**Transitive pinning is enabled** (`CentralPackageTransitivePinningEnabled=true`) so a vulnerable indirect dependency can be forced to a patched version without waiting for the parent package to update. Every such pin must name the advisory it fixes and is removed once the parent ships the fix.

**Adding a package requires an ADR.** The manifest is the allowlist.

**`Directory.Build.props`** applies solution-wide: `net10.0`, nullable enabled, `TreatWarningsAsErrors`, `GenerateDocumentationFile`, and NuGet audit at `mode=all`, `level=low`.

### The manifest

**API — web stack**

| Package | Version | Purpose | Licence |
|---|---|---|---|
| `Microsoft.AspNetCore.OpenApi` | 10.0.10 | Built-in OpenAPI document generation ([ADR-0012](ADR-0012-api-documentation.md)) | MIT |
| `Scalar.AspNetCore` | 2.16.16 | Interactive API docs rendered over that document | MIT |
| `Asp.Versioning.Mvc` | 10.0.0 | URL-segment versioning ([ADR-0015](ADR-0015-api-versioning.md)) | MIT |
| `Asp.Versioning.Mvc.ApiExplorer` | 10.0.0 | Feeds version metadata into OpenAPI/Scalar | MIT |

**API — persistence**

| Package | Version | Purpose | Licence |
|---|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | 10.0.3 | EF Core provider for PostgreSQL ([ADR-0008](ADR-0008-persistence-postgresql-efcore.md)) | PostgreSQL |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.10 | Migrations tooling — design-time only, not shipped | MIT |
| `Microsoft.EntityFrameworkCore` | 10.0.10 | Central runtime alignment pin shared by the API and test projects | MIT |
| `Microsoft.EntityFrameworkCore.Relational` | 10.0.10 | Central relational runtime alignment pin | MIT |
| `Microsoft.EntityFrameworkCore.Abstractions` | 10.0.10 | Central EF abstractions alignment pin | MIT |

**API — authentication and cryptography**

| Package | Version | Purpose | Licence |
|---|---|---|---|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.10 | Access-token validation ([ADR-0001](ADR-0001-token-strategy.md)) | MIT |
| `Microsoft.AspNetCore.Authentication.Google` | 10.0.10 | Google social login (P12) | MIT |
| `AspNet.Security.OAuth.GitHub` | 10.0.0 | GitHub social login (P12) | Apache-2.0 |
| `Isopoh.Cryptography.Argon2` | 2.0.0 | Argon2id password hashing ([ADR-0006](ADR-0006-password-hashing.md)) | **CC0-1.0** ⚠ |
| `Otp.NET` | 1.4.1 | TOTP generation and validation for MFA | MIT ⚠ |
| `Fido2.AspNet` | 4.0.1 | WebAuthn / passkey ceremonies | MIT |
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | 10.0.10 | Shared PostgreSQL Data Protection key ring ([ADR-0021](ADR-0021-data-protection-key-persistence.md)) | MIT |
| `Azure.Extensions.AspNetCore.DataProtection.Keys` | 1.6.3 | Wraps the persisted key ring with Azure Key Vault ([ADR-0027](ADR-0027-azure-production-platform.md)) | MIT |
| `Azure.Identity` | 1.21.0 | Managed-identity credentials for Key Vault and Azure Managed Redis | MIT |

**API — validation, logging, caching**

| Package | Version | Purpose | Licence |
|---|---|---|---|
| `FluentValidation` | 12.1.1 | Request validation ([ADR-0009](ADR-0009-validation-and-mapping.md)) | Apache-2.0 |
| `FluentValidation.DependencyInjectionExtensions` | 12.1.1 | Validator registration in DI | Apache-2.0 |
| `Serilog.AspNetCore` | 10.0.0 | Structured logging ([ADR-0010](ADR-0010-logging-serilog.md)); bundles the console sink | Apache-2.0 |
| `Microsoft.Extensions.Caching.Hybrid` | 10.8.0 | L1/L2 `HybridCache` ([ADR-0029](ADR-0029-distributed-redis-runtime-state.md)) | MIT |
| `Microsoft.Extensions.Caching.StackExchangeRedis` | 10.0.10 | Redis-backed `IDistributedCache` for HybridCache L2 | MIT |
| `Microsoft.Azure.StackExchangeRedis` | 3.2.0 | Entra/managed-identity Redis authentication and token refresh | MIT |

**API — observability and health**

| Package | Version | Purpose | Licence |
|---|---|---|---|
| `OpenTelemetry.Extensions.Hosting` | 1.17.0 | OTel wiring into the host (§28) | Apache-2.0 |
| `OpenTelemetry.Instrumentation.AspNetCore` | 1.17.0 | Inbound request traces and metrics | Apache-2.0 |
| `OpenTelemetry.Instrumentation.Http` | 1.17.0 | Outbound HTTP traces (social provider calls) | Apache-2.0 |
| `OpenTelemetry.Instrumentation.Runtime` | 1.17.0 | GC, thread-pool, exception counters | Apache-2.0 |
| `OpenTelemetry.Exporter.OpenTelemetryProtocol` | 1.17.0 | Optional backend-neutral OTLP export | Apache-2.0 |
| `Npgsql.OpenTelemetry` | 10.0.3 | Npgsql command tracing (§28); version-aligned with Npgsql | PostgreSQL |
| `AspNetCore.HealthChecks.NpgSql` | 9.0.0 | Readiness probe against PostgreSQL | Apache-2.0 |
| `Azure.Monitor.OpenTelemetry.Exporter` | 1.8.3 | Azure Monitor trace/metric exporter ([ADR-0028](ADR-0028-azure-monitor-backend.md)) | MIT |

**Tests**

| Package | Version | Purpose | Licence |
|---|---|---|---|
| `xunit.v3` | 3.2.2 | Test framework ([ADR-0011](ADR-0011-testing-and-ci.md)) | Apache-2.0 |
| `xunit.runner.visualstudio` | 3.1.5 | Test adapter for `dotnet test` and IDEs | Apache-2.0 |
| `Microsoft.NET.Test.Sdk` | 18.8.1 | Test host and discovery | MIT |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.10 | `WebApplicationFactory` for integration tests | MIT |
| `Testcontainers.PostgreSql` | 4.13.0 | Real PostgreSQL per test run | MIT |
| `Testcontainers.Redis` | 4.13.0 | Real Redis for atomic distributed-counter tests | MIT |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.8.0 | `FakeTimeProvider` — session/token expiry without waiting | MIT |
| `Respawn` | 7.0.0 | Database reset between tests | Apache-2.0 |
| `NSubstitute` | 6.0.0 | Test doubles for service unit tests | BSD-3-Clause |
| `coverlet.msbuild` | 10.0.1 | CI coverage collection and scoped coverage gates | MIT |

**Transitive security pin**

| Package | Version | Reason | Licence |
|---|---|---|---|
| `Microsoft.OpenApi` | 2.11.0 | **GHSA-v5pm-xwqc-g5wc** (high): circular schema references can terminate OpenAPI parsing. Vulnerable ≤ 2.7.4, patched 2.7.5. `Microsoft.AspNetCore.OpenApi` 10.0.10 pulls 2.0.0 transitively. | MIT |

**This pin must stay on the 2.x line.** Moving it to 3.x was tried and reverted on 2026-07-22. `Microsoft.AspNetCore.OpenApi` 10.0.10 declares a dependency on `Microsoft.OpenApi` 2.0.0, and its `XmlCommentGenerator` source generator emits `IOpenApiMediaType.Example = …` — a property that is settable in 2.x and **read-only in 3.x**. Pinning 3.9.0 fails the build with two `CS0200` errors in the generated `OpenApiXmlCommentSupport.generated.cs`. The 3.x line fixes the same advisory (patched 3.5.4), so there is no security argument for the jump; 2.11.0 clears it without the breaking change. Revisit only when `Microsoft.AspNetCore.OpenApi` itself targets 3.x.

### Licence notes

All licences are permissive; nothing here carries a copyleft or commercial obligation. Two entries are flagged because their metadata needed resolving rather than reading:

- **`Isopoh.Cryptography.Argon2` is CC0-1.0**, a public-domain dedication rather than a conventional OSS licence — its NuGet metadata still uses the deprecated `licenseUrl` field, and GitHub's detector reports `NOASSERTION`. CC0 imposes no obligations, but some corporate policies treat public-domain dedications as a distinct category, so it is recorded explicitly rather than assumed.
- **`Otp.NET` is MIT**, confirmed from the repository; its NuGet metadata also uses the deprecated `licenseUrl` form and does not state the licence inline.

### Version notes

- **`xunit.v3` is pinned to 3.2.2**, the current stable. A 4.0.0 line exists but is prerelease only; prereleases are not pinned.
- **`AspNetCore.HealthChecks.NpgSql` is 9.0.0** — the library has no 10.x release yet. It is compatible with `net10.0`, and this is the one package lagging the .NET 10 wave. Revisit when 10.x ships.
- **`NSubstitute` is an addition beyond §2's original package list.** §20 unit-tests services in isolation, which needs test doubles; the alternative was hand-written fakes for every service interface. Approved 2026-07-22 alongside P5/P15. No assertion library was added — xUnit's built-in `Assert` is sufficient.

## Alternatives considered

**Per-project `Version` attributes** (the .NET default). Familiar, and fine for a single project. Rejected for a three-project solution: nothing prevents the API project and `tests/IntegrationTests` from resolving different EF Core versions, and auditing versions means reading every `.csproj`.

**Floating versions** (`10.*`, `[10.0,11.0)`). Automatic patch adoption. Rejected outright — the same commit would produce different builds on different days, which destroys reproducibility and means a compromised patch release lands without review.

**`packages.lock.json` instead of CPM.** Locks the full transitive graph, which CPM alone does not. Rejected as the *primary* mechanism because it locks without centralising — versions still live per project — but it is a reasonable future addition on top of CPM, not a replacement.

**Leaving `NuGetAudit` at its defaults** (`mode=direct`, warnings only). Rejected: the advisory that prompted this pin, `Microsoft.OpenApi`, was reachable only transitively. Direct-only auditing would not have seen it.

**Suppressing `NU1903` rather than pinning forward.** Rejected — it treats the symptom. Transitive pinning fixes it at the version, and the pin carries an expiry condition.

**Requiring XML doc comments everywhere** (no `CS1591` suppression). Rejected: with `TreatWarningsAsErrors`, every public DTO property would need a doc comment to compile, producing restated-property-name ceremony rather than documentation.

## Consequences

- The build is reproducible: one commit, one resolved dependency graph.
- **A newly published advisory against any dependency, direct or transitive, fails the build** — `NuGetAudit` at `level=low` plus `TreatWarningsAsErrors`. This is deliberate and it will occasionally interrupt unrelated work. The correct response is to pin the patched version in `Directory.Packages.props`. Silencing the warning is the wrong response; if work is genuinely blocked, a time-boxed `NoWarn` with a tracking note is the escape hatch, not a permanent suppression.
- `Directory.Packages.props` and `Directory.Build.props` sit at the **repository root**, at or above every project. MSBuild discovers these files by walking up from each project directory, so a placement below the test projects would silently fail to govern them.
- A `Version` attribute on any `PackageReference` is now a build error (`NU1008`). That is the mechanism enforcing central management.
- `TreatWarningsAsErrors` means a compiler warning stops the build. Intentional, and the reason nullable is enabled solution-wide rather than per project.
- The manifest lists packages not yet referenced by any project. Unused `PackageVersion` entries are inert — they declare intent and are the reviewed allowlist §3 onwards draws from.
- Dependency update cadence (Dependabot/Renovate weekly, .NET SDK minors monthly) is §29's concern; this ADR fixes the starting point, not the refresh policy.
