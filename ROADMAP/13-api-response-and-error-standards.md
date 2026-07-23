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

- [x] `Extensions/ServiceCollectionExtensions.ProblemDetails.cs`: `AddProblemDetails` with `CustomizeProblemDetails` adding `errorCode`/`correlationId`/`traceId`, plus the non-Development guard that strips the framework's `exception` extension and blanks 5xx `detail`.
- [x] `Exceptions/ExceptionToProblemDetailsMap.cs` — the single status/code table. §14's middleware will be its only caller.
- [x] `Helpers/PagedQueryExtensions.cs`: `ApplySort` (allow-list of typed expressions) and `ToPagedResponseAsync`.
- [x] `Documentation/Errors.md`: full catalogue — code, status, meaning, remediation, and where each code originates.
- [x] `Exceptions/ErrorCodes.cs`, `ProblemTypes.cs`, `ProblemDetailsExtensions.cs`; `Middleware/CorrelationId.cs` (constants only — §14 adds the middleware).
- [x] 9 catalogue guard tests.

## Two gaps found by running it, not by reading it

1. **A 401 had no body at all.** An authorization challenge is not an "error result" — the middleware sets the status and returns, so nothing runs the Problem Details writer. Same for a routing 404 and a method-mismatch 405. `app.UseStatusCodePages()` in the pipeline is what makes "one envelope for every non-2xx" true rather than aspirational.

2. **A body that failed to bind produced a misleading success-shaped response.** §10 suppressed MVC's automatic model-state filter so the validation filter would be the single producer of 400s; the consequence is that a body which cannot deserialize — `{}` against a record with `required` members — leaves a **null action argument**. The filter found nothing to validate, the action ran with a null model, and the caller got a `501` stub response instead of "your request was malformed". The filter now checks `ModelState` first and answers `malformed_request`. Binder messages are deliberately not echoed: they name CLR types and JSON paths.

Also fixed: `501` responses reported `errorCode: internal_error` through the status fallback, which reads as a fault rather than as "not written yet". Now `not_implemented`.

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

**Status: met for every shape the API can currently produce.** Verified against the running app:

```json
401 → {"type":"/errors/unauthorized","title":"Unauthorized","status":401,
       "errorCode":"unauthorized","traceId":"0HNN8LVCSTTL5:00000001"}

400 → {"type":"/errors/malformed_request","title":"The request could not be read.",
       "status":400,"detail":"Could not bind: $, request.",
       "errorCode":"malformed_request","traceId":"…"}

400 → {"type":"/errors/validation_failed","status":400,"errorCode":"validation_failed",
       "errors":{"Email":["Email address is not valid."]},
       "errorCodes":{"Email":["email_invalid"]},"traceId":"…"}
```

101 tests green. Nine of them keep the catalogue honest: every `DomainException` maps to a deliberate status (the 500 fallback arm must stay unreachable), every domain / framework / validation code appears in `Documentation/Errors.md`, `AccountLockedException` maps **identically** to `InvalidCredentialsException`, a non-domain exception's message is withheld unless Development, and a domain exception's message is always shown.

Not yet exercisable: 403, 404, 409 and 500 bodies, because no service throws yet — §12's feature half produces them and §21 asserts them systematically. `correlationId` is absent from every response until §14 adds the middleware that sets it; the reader for it is already in place.

## Questions for the Project Owner

None.
