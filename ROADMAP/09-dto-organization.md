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

- [ ] Create every request/response record implied by the endpoint inventory (~55 files), e.g. `DTOs/Auth/RegisterRequest.cs`, `RegisterResponse.cs`, `LoginRequest.cs`, `LoginResponse.cs`, `MfaChallengeResponse.cs`, `RefreshRequest.cs`, `TokenPairResponse.cs`, `DTOs/Sessions/SessionResponse.cs`, `DTOs/ApiKeys/CreateApiKeyRequest.cs`, `CreateApiKeyResponse.cs`, `ApiKeySummaryResponse.cs`, `DTOs/Admin/AdminUserResponse.cs`, `AdminUserListQuery.cs`, …
- [ ] `DTOs/Common/PagedQuery.cs`, `PagedResponse.cs`.
- [ ] Doc-comment every property that carries security-sensitive data.

## Expected Deliverables

`DTOs/` tree complete for all v1 endpoints.

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

## Questions for the Project Owner

None.
