# 26. CI/CD

## Objective

Every push proven: build, style, tests (unit, integration, security), coverage, vulnerabilities, docs sync, image build — before merge.

## Scope

GitHub Actions CI, Azure CD, staging DAST and scheduled/manual performance workflow.

## Architectural Decisions

- Single `ci.yml` on PR + main push; ubuntu-latest (Docker available for Testcontainers).
- Stages: restore → `dotnet format --verify-no-changes` → build (warnings as errors) → unit
  crypto/validator coverage gate → PostgreSQL/Redis integration + security + token-service
  coverage gate (§20–§22, docs/OpenAPI included) → transitive vulnerability/secret gates →
  digest-pinned application-image vulnerability gate and container smoke → portable EF
  migration bundle artifact.
- Branch protection: PRs only into main, CI required, no force push. Azure CD uses OIDC and
  GitHub Environments; a trusted main push deploys only after CI succeeds and remains the
  current main head, and production dispatch waits on environment approval. External actions
  and scanners are immutable-SHA / digest pinned; pushed images carry SBOM and max-level
  provenance.

## Technology Decisions Requiring Approval

None; P14 is resolved by ADR-0027.

## Tasks

- [x] `.github/workflows/ci.yml` implementing the stage list with caching (NuGet, Docker layers).
- [x] Coverage reporting (coverlet → summary). Crypto + validators enforce 85%; the real-
  PostgreSQL integration job independently enforces 85% on `Services/Tokens` and measured
  94.23% on 2026-08-02.
- [x] `.github/dependabot.yml` (Dependabot, aligned with §16).
- [x] Branch-protection setup documented in `Documentation/Operations/CI.md`; applying it requires the external GitHub repository/owner.
- [x] Badge row in `README.md`.
- [x] `.github/workflows/deploy.yml`: ACR image, Bicep, migration job, readiness and ZAP.
- [x] `.github/workflows/performance.yml`: staging k6 budgets with guaranteed limit restore.

**Latest local verification 2026-08-02:** Release build and 274 unit + 105 integration tests
are green; the suite includes real PostgreSQL and Redis. Format, frontend lint/type/test/build,
repeatable static output, scoped coverage, live NuGet and pnpm advisory lookups, secret scan,
digest-pinned Trivy 0.72.0 image scan (zero HIGH/CRITICAL findings, including unfixed),
Compose/image/non-root/readiness, model-snapshot drift and the self-contained Linux migration
bundle all pass. Bicep compiles with the official Azure CLI and all workflow YAML parses.
Live workflows, branch rules, synthetic gate violations, badge resolution and uploaded
artifacts require a GitHub push, so the Definition of Done is not yet claimed.

## Expected Deliverables

Working CI, Azure CD and staging security/performance workflows, plus operations documentation.

## Dependencies

§20–§22 (suites), §24 (image), §8 (bundle). Repo must be pushed to GitHub (owner action).

## Security Considerations

CI secrets: none needed for CI itself (Testcontainers is local); when CD arrives, deploy credentials go to GitHub Environments with reviewers, never repo secrets in plain workflows.

## Testing Requirements

CI is the test aggregator; a deliberately-broken PR (each gate) verified once as pipeline acceptance.

## Documentation Requirements

CI doc: stage map, how to reproduce each gate locally.

## Definition of Done

All gates green on main; each gate proven to fail on a synthetic violation; badges live.

## Questions for the Project Owner

1. Will the repo live on github.com under your account/org (needed for branch protection + Actions)?
