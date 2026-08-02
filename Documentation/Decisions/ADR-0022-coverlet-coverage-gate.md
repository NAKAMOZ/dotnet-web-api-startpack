# ADR-0022: Coverlet for CI coverage enforcement

- **Status:** Accepted
- **Date:** 2026-07-26
- **Workstreams:** §20, §26

## Context

CI must generate machine-readable line coverage and fail below the targeted 85% floor.
The test platform can collect a proprietary coverage artifact, but it does not provide an
MSBuild threshold gate or Cobertura output by itself.

## Decision

Reference `coverlet.msbuild` as a private test dependency in both test projects. CI emits
Cobertura and applies an 85% line threshold to crypto/validators in the unit job and to
`Services/Tokens` in the real PostgreSQL integration job. This keeps EF-backed rotation and
session behavior in the gate without replacing the provider with a misleading fake.

## Alternatives considered

- Binary platform coverage alone: rejected because it cannot enforce the scoped gate.
- A global percentage: rejected because high coverage in plumbing can hide an untested
  authentication decision.

## Consequences

- The dependency never flows into the API or published image.
- Coverage is a merge gate rather than a dashboard-only number.
- The token gate measured **94.23% line coverage** on the 2026-08-02 release-candidate run,
  above the unchanged 85% threshold.
