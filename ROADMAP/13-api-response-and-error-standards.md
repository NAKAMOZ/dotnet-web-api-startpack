# 13. API Response and Error Standards

## Objective

One canonical shape for every success, error, and page — RFC 9457 everywhere, stable machine-readable codes.

## Scope

ProblemDetails customization, error catalog, pagination/filtering/sorting conventions, idempotency semantics.

## Architectural Decisions

- Errors: RFC 9457 `application/problem+json` with extensions `errorCode` (stable string), `correlationId`, `traceId`; `type` is a URI under `/errors/<code>` documented in `Documentation/Errors.md`. Validation failures use `ValidationProblemDetails` with per-field `errors`.
- 401 vs 403: 401 = missing/invalid/expired credentials (with `WWW-Authenticate`); 403 = authenticated but not permitted. Enumeration-sensitive endpoints (reset request, register) return success-shaped 200/202 regardless of account existence (§16).
- Pagination: query `page` (1-based) + `pageSize` (default 20, max 100) + `sort=field:asc|desc` (whitelisted); response = `PagedResponse<T>` envelope. Applies to `GET /admin/users`, `GET /admin/audit-logs` (and any future list).
- Idempotency: PUT/DELETE naturally idempotent (revoking a revoked session → 204). POST `/auth/refresh` is deliberately non-idempotent (rotation) — replays 401 by design, documented. No `Idempotency-Key` machinery in v1 (no payment-like semantics exist); noted as future work.

## Technology Decisions Requiring Approval

None.

## Tasks

- [ ] `Extensions/ServiceCollectionExtensions.ProblemDetails.cs`: `AddProblemDetails` with `CustomizeProblemDetails` adding `errorCode`/`correlationId`/`traceId`.
- [ ] `Exceptions/` → status/code mapping table implemented in `Middleware/ExceptionHandlingMiddleware` (§14) via a single `ExceptionToProblemDetailsMap`.
- [ ] `Helpers/PagedQueryExtensions.cs`: `ApplyPaging`/`ApplySort` (whitelist-driven) used by list services.
- [ ] `Documentation/Errors.md`: full error-code catalog (code, HTTP status, meaning, remediation).

## Expected Deliverables

ProblemDetails extension, exception map, paging helpers, error catalog doc.

## Dependencies

§12 (exception types). Feeds §14.

## Security Considerations

Problem details never include exception messages/stack traces in non-Development environments; `errorCode` granularity reviewed so codes themselves don't leak account state on enumeration-sensitive routes.

## Testing Requirements

§21 asserts the envelope (fields present, no stack traces) on representative 400/401/403/404/409/429/500 responses.

## Documentation Requirements

Error catalog; every endpoint doc's error table uses only cataloged codes.

## Definition of Done

Every non-2xx response in the integration suite is valid RFC 9457 with a cataloged `errorCode`.

## Questions for the Project Owner

None.
