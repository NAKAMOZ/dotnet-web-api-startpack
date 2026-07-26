# 26. CI/CD

## Objective

Every push proven: build, style, tests (unit, integration, security), coverage, vulnerabilities, docs sync, image build — before merge.

## Scope

GitHub Actions CI pipeline; CD skeleton deferred to P14.

## Architectural Decisions

- Single `ci.yml` on PR + main push; ubuntu-latest (Docker available for Testcontainers).
- Stages: restore → `dotnet format --verify-no-changes` → build (warnings as errors) → unit tests + coverage gate (§20) → integration + security suites (§21/§22, includes docs-sync §19 and OpenAPI snapshot §18) → `dotnet list package --vulnerable --include-transitive` gate → secret-pattern grep (§25) → docker image build + smoke (§24) → EF migration bundle artifact (§8).
- Branch protection: PRs only into main, CI required, no force push. CD job stub created but disabled pending P14.

## Technology Decisions Requiring Approval

None for CI (GitHub Actions approved); CD blocked on P14.

## Tasks

- [x] `.github/workflows/ci.yml` implementing the stage list with caching (NuGet, Docker layers).
- [x] Coverage reporting (coverlet → summary in PR). Crypto + validators enforce 85% and currently measure 98.49%; ADR-0022 keeps the incomplete whole-token namespace gate visible.
- [x] `.github/dependabot.yml` (Dependabot, aligned with §16).
- [x] Branch-protection setup documented in `Documentation/Operations/CI.md`; applying it requires the external GitHub repository/owner.
- [x] Badge row in `README.md`.

**Locally verified 2026-07-26:** restore/build/format, 202 unit tests, 72 integration tests
(47 security), the 85% scoped coverage gate, secret scan, Compose validation, image build,
non-root execution, and readiness smoke. The workflow itself, branch rules, synthetic gate
violations, badge resolution, and uploaded migration artifact require a GitHub push, so the
Definition of Done is not yet claimed.

## Expected Deliverables

Working CI on the GitHub repo, CI doc, disabled CD stub.

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
