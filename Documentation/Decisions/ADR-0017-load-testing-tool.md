# ADR-0017: k6 as the Load-Testing Tool

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** **Resolves pending decision P15** (`ROADMAP/00-overview.md`)
- **Affects:** §23 (performance and load testing), §24 (compose), §26 (CI)

## Context

§23 needs to establish performance budgets and prove the API holds them. The load profile for an auth service is distinctive: login is deliberately slow because Argon2id is deliberately slow ([ADR-0006](ADR-0006-password-hashing.md)), while token refresh and validation must be fast and are called far more often.

That combination is exactly what makes load testing worth doing here. Argon2id's memory cost is a server-side resource cost, so the same parameters that make offline cracking impractical can, under concurrent login load, become a denial-of-service vector against ourselves. Only measurement settles where that line sits.

## Decision

**k6** for load and performance testing.

Scenarios are authored in JavaScript and live **outside the .NET solution** — they are operational tooling, not part of the build. k6 runs as a container, so it fits the existing docker-compose setup ([ADR-0011](ADR-0011-testing-and-ci.md)) with no new toolchain on developer machines.

Scenarios to build in §23, following from the context above:

- **Login under concurrency** — the Argon2id cost curve; the parameter-tuning evidence.
- **Refresh throughput** — the highest-volume authenticated path.
- **Access-token validation** — should be stateless and flat; a rising curve indicates an unintended database call on the hot path.

## Alternatives considered

**NBomber.** C#-native, so scenarios live in the solution and can reuse DTOs and helpers, and contributors need no second language. Genuinely attractive for a .NET team. Rejected on ecosystem size and on the same reasoning that keeps load tests out of the solution: performance scenarios are operational artefacts with a different lifecycle from unit and integration tests, and coupling them to the solution's compilation invites treating them as tests that must pass on every build.

**Apache JMeter.** Mature and capable. Rejected on authoring experience — XML-configured test plans are harder to review in a pull request than a JavaScript file, and reviewability matters for artefacts that encode performance budgets.

**Locust.** Python-authored and pleasant to write. Rejected as it would introduce a Python toolchain for this one purpose; k6 needs only a container.

**`wrk` or `hey`.** Excellent for a single-endpoint smoke measurement. Rejected as insufficient — the scenarios above need multi-step flows carrying tokens between requests, which flat HTTP hammering cannot express.

**Deferring the decision to §23.** Rejected because §23 sits late in the plan and the tool choice affects §24's compose file and §26's CI job, both of which land earlier.

## Consequences

- Load scenarios are written in JavaScript, not C#. A contributor touching them works in a second language — accepted, given the scenarios are few and short.
- k6 scripts sit outside the solution, so they are **not compiled and not covered by `TreatWarningsAsErrors`** ([ADR-0013](ADR-0013-package-manifest.md)). They can rot silently if an endpoint changes. §23 must state where they live and that they are reviewed when the endpoints they exercise change.
- Load tests do **not** run on every CI build — they need a realistic environment and meaningful duration. §26 runs them on a schedule or on demand, not per pull request.
- Performance budgets are only meaningful against fixed hardware. Results from a developer laptop and from CI are not comparable, and §23 must record the environment alongside the numbers or the budgets are noise.
- The Argon2id tuning outcome feeds back into [ADR-0006](ADR-0006-password-hashing.md): if measurement shows the chosen parameters make concurrent login a self-inflicted denial of service, the parameters change and the ADR is superseded, not quietly edited.
- No NuGet package is added — k6 is not a .NET dependency and does not appear in `Directory.Packages.props`.
