# 18. Scalar and OpenAPI Configuration

## Objective

A complete, accurate OpenAPI document per API version, served through Scalar for interactive exploration.

## Scope

OpenAPI generator configuration, document transformers, Scalar wiring, exposure policy.

## Architectural Decisions

- Built-in `Microsoft.AspNetCore.OpenApi` generator (already referenced) + `Scalar.AspNetCore` UI at `/scalar/{version}`; document at `/openapi/{version}.json`.
- Document transformers add: security schemes (bearer JWT, cookie, API key), server info, contact/license metadata; operation-level security requirements derived from `[RequirePermission]`/`[AllowAnonymous]` via an operation transformer — auth requirements in the docs can never drift from the attributes.
- XML doc comments (`GenerateDocumentationFile` from §2) feed summaries/descriptions; `[ProducesResponseType]` (§11) feeds response schemas.
- Versioned documents via `Asp.Versioning.Mvc.ApiExplorer` (P2).
- Exposure: dev + staging; disabled in production (P16).

## Technology Decisions Requiring Approval

P16.

## Tasks

- [x] `Extensions/ServiceCollectionExtensions.OpenApi.cs`: `AddOpenApi` per version + transformers (`Configuration/OpenApi/SecuritySchemeTransformer.cs`, `AuthRequirementOperationTransformer.cs`).
- [x] `Extensions/ApplicationBuilderExtensions.OpenApi.cs`: `MapOpenApi` + `MapScalarApiReference`, environment-gated per the P16 recommendation.
- [x] XML summary on every action + DTO. `GenerateDocumentationFile` remains enabled and the built-in generator consumes the comments.
- [x] Verify generated document: all 43 inventory operations present; bearer, cookie and API-key schemes emitted; operation security derived from endpoint metadata; §19 sync and OpenAPI contract tests green.

## Expected Deliverables

OpenAPI extensions + transformers; Scalar UI browsable across all endpoints with working auth (paste token / cookie mode).

## Dependencies

§11 (annotations), P2.

## Security Considerations

Prod exposure disabled (P16); the OpenAPI document reveals the full attack surface — staging access should sit behind network controls (§27 checklist).

## Testing Requirements

Implemented ahead of §21: `OpenApiContractTests` asserts parseability, the 43-operation inventory, schemes, anonymous/protected security, Scalar availability in Staging, and no production exposure. §19 asserts full route + method set equality.

## Documentation Requirements

Scalar usage note in `README.md`.

## Definition of Done

Scalar renders all endpoints with correct schemas, auth badges, and examples; snapshot test green.

## Questions for the Project Owner

1. Approve dev+staging-only exposure (P16)?
