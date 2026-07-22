# ADR-0009: FluentValidation for Request Validation, Manual Extension Methods for Mapping

- **Status:** Accepted
- **Date:** 2026-07-22
- **Deciders:** Project owner
- **Source:** `ROADMAP/00-overview.md` approved-decisions table, rows *Validation* and *Mapping*
- **Affects:** §9 (DTOs), §10 (validation), §12 (services), §13 (error standards), §20 (unit tests)

## Context

Two adjacent concerns sit between the HTTP boundary and the domain: checking that an incoming request is well-formed, and converting between DTOs and entities. Both are boilerplate-adjacent, and both have a popular library that promises to make them disappear.

They are grouped in one ADR because they are the same shape of decision — how much magic to accept at the DTO boundary — and they were resolved in opposite directions, which is worth recording together.

## Decision

**Validation: FluentValidation.** One validator class per request DTO, in `Validators/`, mirroring the `DTOs/<Feature>/` layout.

Validation runs through **our own action filter** (§10), not the deprecated `FluentValidation.AspNetCore` auto-validation package. Failures are converted to RFC 9457 Problem Details with per-field detail ([ADR-0007](ADR-0007-runtime-and-api-style.md)).

**Mapping: manual extension methods**, per feature, in `Mappings/` — e.g. `UserMappingExtensions.ToResponse(this User user)`. **AutoMapper is not used.**

## Alternatives considered

**Data annotations for validation.** Built in, no dependency. Rejected: attribute-based rules cannot express conditional or cross-field logic without escaping to `IValidatableObject`, and this API has plenty of both (password confirmation, MFA-conditional fields, mutually exclusive identifiers). Rules also end up interleaved with the DTO's shape rather than separated from it.

**`FluentValidation.AspNetCore` auto-validation.** Rejected on two counts: the package is deprecated, and implicit validation hides *when* validation runs. An explicit filter makes the pipeline position visible and puts error-response shaping under our control.

**AutoMapper.** Rejected for two specific reasons, recorded here because "we didn't like it" is not a decision record:

1. **Licensing** — AutoMapper moved to a commercial license, which is a supply-chain and cost consideration rather than a technical one, and exactly the class of surprise §2's per-package license note exists to catch.
2. **Runtime opacity** — convention-based mapping fails at runtime, not compile time. Renaming an entity property silently stops populating a response field, and the test that would have caught it is the one nobody wrote. In an auth API, a silently unpopulated field can mean an unpopulated `roles` array or a missing `emailVerified` flag.

**Mapster or similar source generators.** A reasonable middle ground with compile-time generation. Rejected as an additional dependency for a problem hand-written methods already solve, at this project's scale.

## Consequences

- Mapping code is longer and hand-written. Accepted: it is greppable, debuggable, breaks the build when a property is renamed, and needs no configuration DSL to understand.
- Every request DTO needs a validator class — one per DTO is mandated, so a DTO without one is an incomplete slice, caught at review.
- Validators are plain classes with no HTTP dependency, so they unit test directly (§20) without a test server.
- The validation filter is a single choke point for request-error shape, which is what keeps 400 responses uniform across 40+ endpoints.
- Two directories (`Validators/`, `Mappings/`) mirror the `DTOs/` tree, so a feature's DTO, validator, and mapper are found by the same path in three places rather than by search.
- Mapping methods must never expose sensitive fields — `PasswordHash`, `SecretEncrypted`, `KeyHash`, `TokenHash` never appear in a response DTO. Manual mapping makes that an inspectable property of the code; convention-based mapping would have made it a configuration question.
