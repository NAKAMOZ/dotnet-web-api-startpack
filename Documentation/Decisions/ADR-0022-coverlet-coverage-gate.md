# ADR-0022: Coverlet for CI coverage enforcement

- **Status:** Accepted
- **Date:** 2026-07-26
- **Workstreams:** §20, §26

## Context

CI must generate machine-readable line coverage and fail below the targeted 85% floor.
The test platform can collect a proprietary coverage artifact, but it does not provide an
MSBuild threshold gate or Cobertura output by itself.

## Decision

Reference `coverlet.msbuild` as a private test dependency in `UnitTests`. CI emits
Cobertura and applies an 85% line threshold to the currently unit-testable crypto and
validator namespaces. The token namespace's own threshold becomes mandatory when the
remaining EF-backed token behavior has executable integration coverage.

## Alternatives considered

- Binary platform coverage alone: rejected because it cannot enforce the scoped gate.
- A global percentage: rejected because high coverage in plumbing can hide an untested
  authentication decision.

## Consequences

- The dependency never flows into the API or published image.
- Coverage is a merge gate rather than a dashboard-only number.
- The temporary token-threshold gap remains visible in §20/§26 rather than being replaced
  with a weaker percentage.
