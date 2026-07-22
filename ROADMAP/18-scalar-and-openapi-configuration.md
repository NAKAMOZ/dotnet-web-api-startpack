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

- [ ] `Extensions/ServiceCollectionExtensions.OpenApi.cs`: `AddOpenApi` per version + transformers (`Configuration/OpenApi/SecuritySchemeTransformer.cs`, `AuthRequirementOperationTransformer.cs`).
- [ ] `Extensions/ApplicationBuilderExtensions.OpenApi.cs`: `MapOpenApi` + `MapScalarApiReference`, environment-gated per P16.
- [ ] XML summary on every action + DTO (enforced: build warning as error for missing docs on public API surface).
- [ ] Verify generated document: every inventory route present, every security requirement correct (manual review + §19 sync test).

## Expected Deliverables

OpenAPI extensions + transformers; Scalar UI browsable across all endpoints with working auth (paste token / cookie mode).

## Dependencies

§11 (annotations), P2.

## Security Considerations

Prod exposure disabled (P16); the OpenAPI document reveals the full attack surface — staging access should sit behind network controls (§27 checklist).

## Testing Requirements

§21: snapshot test asserts the document parses and contains the full inventory (route + method set equality).

## Documentation Requirements

Scalar usage note in `README.md`.

## Definition of Done

Scalar renders all endpoints with correct schemas, auth badges, and examples; snapshot test green.

## Questions for the Project Owner

1. Approve dev+staging-only exposure (P16)?
