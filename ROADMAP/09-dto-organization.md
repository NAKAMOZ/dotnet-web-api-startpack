# 9. DTO Organization

## Objective

Per-feature request/response contracts — one record per file, EF entities never serialized.

## Scope

DTO records for every endpoint in the inventory, shared paging primitives.

## Architectural Decisions

- `record` types with `required init` properties; requests and responses always separate types even when shapes coincide today.
- Directory mirrors controllers: `DTOs/Auth/`, `DTOs/Sessions/`, `DTOs/EmailVerification/`, `DTOs/PasswordReset/`, `DTOs/Mfa/`, `DTOs/Passkeys/`, `DTOs/SocialAuth/`, `DTOs/ApiKeys/`, `DTOs/Users/`, `DTOs/Admin/`, `DTOs/Common/`.
- `DTOs/Common/`: `PagedQuery` (page, pageSize, sort), `PagedResponse<T>` (items, page, pageSize, totalCount), shared value DTOs (`SessionSummary`).
- Secrets appear in exactly one response each and never again: API-key secret only in the creation response; TOTP secret only in the enrollment response; recovery codes only at generation. Response DTO doc-comments state this.

## Technology Decisions Requiring Approval

None.

## Tasks

- [x] Create every request/response record implied by the endpoint inventory — **47 files**, not the estimated ~55. The difference is not missing coverage: endpoints that return `204 No Content` (logout, delete-account, revoke-one-session, unlink-account, delete-passkey, delete-role-grant, disable-TOTP) need no response type, and `LoginResponse` is reused by the four flows that all end in a live session (password, MFA completion, social callback, passkey assertion) rather than being copied per feature.
- [x] `DTOs/Common/PagedQuery.cs`, `PagedResponse.cs`.
- [x] Doc-comment every property that carries security-sensitive data.
- [x] `tests/UnitTests/DTOs/DtoContractTests.cs` — the reflection guard the Definition of Done requires (moved forward from §20, since the DoD names it here).

## Per-feature file count

| Namespace | Files | Namespace | Files |
|---|---|---|---|
| `DTOs/Auth/` | 9 | `DTOs/Users/` | 4 |
| `DTOs/Admin/` | 7 | `DTOs/Common/` | 4 |
| `DTOs/Passkeys/` | 7 | `DTOs/Mfa/` | 3 |
| `DTOs/ApiKeys/` | 3 | `DTOs/Sessions/` | 2 |
| `DTOs/EmailVerification/` | 2 | `DTOs/PasswordReset/` | 2 |
| `DTOs/SocialAuth/` | 2 | `DTOs/WellKnown/` | 2 |

## Decisions taken here

1. **`DTOs/WellKnown/`** is a twelfth directory beyond the eleven the roadmap lists. `GET /.well-known/jwks.json` is in the endpoint inventory and needs `JwksResponse` + `JsonWebKeyResponse`, whose property names are the RFC 7517 short forms (`kty`, `kid`, `crv`) set explicitly with `[JsonPropertyName]` — a verifier that cannot find `kid` simply fails, so the serializer's casing policy must not decide them.
2. **WebAuthn payloads are `JsonElement`, not typed records.** The option and assertion shapes are large and versioned by the W3C spec; re-declaring them here would put a second, drifting copy of Fido2NetLib's model into the public contract. §12 fills them from the library, and the library's types stay out of every API signature.
3. **`AdminUserListQuery` and `AuditLogQuery` inherit `PagedQuery`**, which is therefore `record` rather than `sealed record` — the only unsealed type in the tree.
4. **`LinkedAccountResponse` omits `ProviderAccountId`**, and `AdminUpdateUserRequest` has no way to *impose* a lockout or set a password. Both are omissions on purpose, documented in the types themselves.

## Expected Deliverables

`DTOs/` tree complete for all v1 endpoints (47 files) plus the contract guard tests.

## Dependencies

§4 (token payloads), endpoint inventory. Blocks §10, §11.

## Security Considerations

No DTO ever exposes: password hashes, token hashes, TOTP secrets after enrollment, internal ids of other users. `TokenPairResponse` omits token fields entirely in cookie mode (nulled and `[JsonIgnore(WhenWritingNull)]`).

## Testing Requirements

§20: mapping tests assert no sensitive entity property reaches any response DTO (reflection-based guard test).

## Documentation Requirements

Request/response schemas in endpoint docs (§19) generated from these types' shapes.

## Definition of Done

Every inventory endpoint has typed request/response DTOs; guard test green.

**Status: met.** All 43 inventory endpoints have typed contracts (or return `204`, which needs none). Five guard tests green, 37 in the suite overall:

- No DTO carries a property named `PasswordHash`, `TokenHash`, `CodeHash`, `KeyHash`, `SecretEncrypted`, `PrivateKeyProtected` or `SecurityStamp` — by name, because the type system cannot tell a hash from a display name.
- No DTO references a type from `Api.Models`, directly or through a collection.
- Every DTO is a `record`.
- No type is named as both request and response.
- All twelve feature namespaces are present — this is also what stops the other four passing vacuously on an empty reflection result.

## Questions for the Project Owner

None.
