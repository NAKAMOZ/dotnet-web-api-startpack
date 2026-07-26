# Error Catalog

**Status:** Written 2026-07-23 · **Workstream:** §13 · **Consumed by:** §14 (middleware), §19 (endpoint docs), §21 (assertions)

Every non-2xx response this API can produce, as RFC 9457 `application/problem+json`. Endpoint documentation (§19) may only cite codes listed here.

---

## 1. The envelope

```json
{
  "type": "/errors/invalid_credentials",
  "title": "Authentication failed.",
  "status": 401,
  "detail": "Invalid email or password.",
  "errorCode": "invalid_credentials",
  "correlationId": "b4f0…",
  "traceId": "00-8a3d…-01"
}
```

| Member | Always present | Meaning |
|---|---|---|
| `type` | ✅ | `/errors/<errorCode>`. A stable identifier, resolved against the request base URI |
| `title` | ✅ | Short human-readable summary. **Prose — may be reworded at any time** |
| `status` | ✅ | HTTP status, repeated in the body per RFC 9457 |
| `detail` | — | Specific to this occurrence. Withheld for 5xx outside Development |
| `errorCode` | ✅ | **The contract.** Stable `snake_case` identifier — branch on this, never on `title` |
| `errorCodes` | validation only | Per-field codes, parallel to the standard `errors` member |
| `correlationId` | when set (§14) | Ties the response to log lines and audit rows for the same request |
| `traceId` | ✅ | ASP.NET Core trace identifier, for distributed traces (§28) |

**`title` and `detail` are prose; `errorCode` is the interface.** A client that keys off the message breaks on the first copy edit, and localised clients break silently.

### The one exemption

`/health/live` and `/health/ready` (§28) answer `text/plain` — `Healthy` or `Unhealthy` — not Problem Details. Orchestrator probes read a status code and a word, are not API clients, and never see an `errorCode`. This is the complete list of exempt endpoints; anything else answering a non-2xx without the envelope above is a defect. See `Documentation/Architecture/Pipeline.md` §1.

---

## 2. Authentication and authorization

| Code | Status | Meaning | Remediation |
|---|---|---|---|
| `invalid_credentials` | 401 | Authentication failed | Re-enter credentials |
| `token_reuse_detected` | 401 | A spent refresh token was presented again; **the session was revoked** | Log in again. Investigate — this is a theft signal |
| `unauthorized` | 401 | No credentials, or credentials that did not authenticate | Authenticate, then retry |
| `forbidden` | 403 | Authenticated, but lacking the required permission | Nothing the caller can do — do not prompt |
| `step_up_required` | 403 | Authenticated and permitted, but not *recently* authenticated | Re-authenticate, then retry. **Distinct from `forbidden` so a client prompts rather than logging the user out** |
| `csrf_validation_failed` | 403 | A cookie-authenticated state-changing request arrived without a valid, session-bound CSRF token (§14) | Fetch `GET /api/v1/auth/csrf`, echo the value in `X-CSRF-Token`, retry once |
| `invalid_token` | 400 | A verification, reset, MFA or challenge token did not resolve | Request a new one |

> **`invalid_credentials` deliberately conflates four cases**: unknown email, wrong password, locked account, and an account with no password at all. They are identical in code, title, detail and — via the dummy-hash path in §12 — in timing. Splitting any of them apart creates an account-enumeration oracle. `account_locked` exists as an internal exception for the audit trail and **never reaches a client**.

---

## 3. Validation

| Code | Status | Meaning |
|---|---|---|
| `validation_failed` | 400 | One or more fields failed structural validation |
| `malformed_request` | 400 | The body could not be parsed, or a value could not be bound |

`validation_failed` responses carry both standard `errors` (messages, per field) and `errorCodes` (stable codes, per field):

```json
{
  "type": "/errors/validation_failed",
  "status": 400,
  "errorCode": "validation_failed",
  "errors":     { "Email": ["Email address is not valid."] },
  "errorCodes": { "Email": ["email_invalid"] }
}
```

Per-field codes are defined in `Validators/Common/ValidationErrorCodes.cs`:

`required` · `too_long` · `out_of_range` · `email_invalid` · `email_too_long` · `password_too_short` · `password_too_long` · `password_too_common` · `password_predictable_pattern` · `password_contains_email` · `password_unchanged` · `page_out_of_range` · `page_size_out_of_range` · `sort_field_not_allowed` · `scope_unknown` · `scopes_empty` · `code_malformed` · `expiry_in_past` · `state_missing` · `callback_incomplete` · `range_inverted`

---

## 4. Resource state

| Code | Status | Meaning |
|---|---|---|
| `not_found` | 404 | The resource does not exist **or does not belong to the caller** |
| `email_already_registered` | 409 | Registration hit an existing address ⚠️ *see below* |
| `method_not_allowed` | 405 | Route exists, method does not |
| `not_acceptable` | 406 | No representation matches `Accept` |
| `unsupported_media_type` | 415 | `Content-Type` is not `application/json` |

> **404 covers both "absent" and "not yours", deliberately.** Answering 403 for the second confirms the resource exists, which lets an attacker enumerate ids by reading status codes. Own-resource routes scope the lookup to the caller and treat a miss as absence.

> ⚠️ **`email_already_registered` is internal-only on the anonymous registration path — resolved by §16.** Returning it there discloses to an anonymous caller which addresses are registered, the same oracle the password-reset flow refuses to provide. `POST /api/v1/auth/register` answers `202` for both cases and sends a "someone tried to register your address" notice instead; the exception is caught and converted before it reaches the client, exactly like `account_locked`. It still surfaces as a genuine `409` where the caller is already authenticated and already knows the address exists — linking a social account to a taken email. See `Documentation/Security/Enumeration.md` §3.

---

## 5. Infrastructure

| Code | Status | Meaning |
|---|---|---|
| `rate_limited` | 429 | Rate limit exceeded (§17). Carries `Retry-After` |
| `not_implemented` | 501 | The route exists and its contract is fixed; the service behind it is not written yet (§12). Transitional — no code should depend on it |
| `request_cancelled` | 499 | The client disconnected before the response. Not a real HTTP status — it is a log-facing value, and nothing is written to a socket nobody is holding |
| `internal_error` | 500 | Unhandled fault |

**`internal_error` never carries a `detail` outside Development**, and the framework's `exception` extension — message, type name, stack trace — is stripped from every problem response. Stack traces name internal paths, dependency versions and query shapes; an exception message may carry a connection string outright.

---

## 6. Where each code comes from

| Source | Codes |
|---|---|
| `DomainException` subclasses (`Exceptions/`) | `invalid_credentials`, `account_locked`*, `email_already_registered`, `token_reuse_detected`, `invalid_token`, `not_found`, plus per-case `ConflictException` codes |
| `Exceptions/ErrorCodes.cs` | `validation_failed`, `unauthorized`, `forbidden`, `step_up_required`, `csrf_validation_failed`, `malformed_request`, `rate_limited`, `internal_error` |
| `Filters/CsrfProtectionFilter` | `csrf_validation_failed` |
| `Validators/Common/ValidationErrorCodes.cs` | the per-field codes in §3 |
| Status fallback (`AddProblemDetailsStandards`) | `not_found`, `method_not_allowed`, `not_acceptable`, `unsupported_media_type`, `not_implemented` — for responses the framework produces with no exception behind them |
| `Filters/ValidationFilter` | `validation_failed`, `malformed_request` |

\* internal only — converted to `invalid_credentials` before it reaches a client.

Mapping from exception to status lives in **one** place: `Exceptions/ExceptionToProblemDetailsMap.cs`. A service never constructs a response, and a controller never maps an error — otherwise the same failure thrown from three places acquires three statuses, one of which is wrong.

---

## 7. Conventions this catalogue depends on

**401 vs 403.** 401 means *"I do not know who you are"* — missing, invalid or expired credentials, always with `WWW-Authenticate`. 403 means *"I know who you are and the answer is no"*. A client retries after authenticating on 401; on 403 it must not, because it will get the same answer forever. `step_up_required` is the one 403 a client *should* respond to by re-authenticating, which is exactly why it has its own code.

**Enumeration-sensitive endpoints answer success-shaped.** `POST /password-reset/request` returns `202` for any well-formed address, registered or not. No error code exists for "no such account", because emitting one would be the oracle.

**Idempotency.** `PUT` and `DELETE` are naturally idempotent — revoking an already-revoked session is `204`, not `404`. `POST /auth/refresh` is deliberately **not** idempotent: rotation is the point, and a replayed token is treated as theft. No `Idempotency-Key` machinery in v1; nothing here has payment-like semantics. Listed as future work in §29.

**Pagination.** `page` (1-based, default 1) + `pageSize` (default 20, **max 100**) + `sort=field[:asc|desc]` against a per-endpoint allow-list. Responses use the `PagedResponse<T>` envelope. Out-of-range values are **rejected, not clamped** — a silently clamped page size answers a different question than the one asked, and the caller never learns it.
