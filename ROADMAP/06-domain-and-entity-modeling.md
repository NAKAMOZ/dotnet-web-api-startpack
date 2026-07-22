# 6. Domain and Entity Modeling

## Objective

Implement the 13-entity model — one file per entity, nullable-aware, no EF leakage into DTOs.

## Scope

Entity classes only; EF configuration is §7.

## Architectural Decisions

- IDs: `Guid` v7 (`Guid.CreateVersion7()`) — time-ordered, index-friendly in PostgreSQL.
- All timestamps `DateTimeOffset`, always UTC, provided by injected `TimeProvider` (never `DateTime.Now`) for testability.
- Common audit fields via `IAuditableEntity` interface (`CreatedAt`, `UpdatedAt`) set by a `SaveChanges` interceptor (§7) — no base-class inheritance forced on entities that don't fit.
- Enums (`VerificationTokenType`, `SessionRevocationReason`, `SigningKeyStatus`, `AuditEventType`) each in their own file under `Models/Enums/`.
- Navigation properties defined on both sides only where queries need them; collections initialized.

## Technology Decisions Requiring Approval

None.

## Tasks

- [x] One file per entity in `Models/` exactly as listed in the entity table (13 files).
- [x] `Models/Enums/` — one file per enum (4 files, plus `AuthenticationMethod` relocated — see Deviations).
- [x] `Models/IAuditableEntity.cs`.
- [x] XML doc comments on every security-relevant property (what is hashed, what is plaintext, why).

## Deviations from the original specification

Recorded here rather than silently applied; all three are owner-visible.

1. **`AuthenticationMethod` and `SessionRevocationReason` moved** from `Services/Tokens/` to
   `Models/Enums/`. `Session` persists both, and an entity referencing the service layer
   inverts the dependency direction. `Services/Tokens/*.cs` now carries
   `using Api.Models.Enums;`. File count is therefore 19, not 18 — 18 authored, one moved in.
2. **Three columns added to `Session`** (`AuthenticatedAt`, `AuthenticationMethods`,
   `SecurityStamp`), plus `SigningKey.RetiringAt`. Each is required by a flow §4 already
   specified; the rationale table is in `00-overview.md` under the entity table.
3. **`VerificationToken.UserId` is nullable**, for `PasskeyAuthenticationChallenge` only —
   that ceremony begins before any user is identified (Authentication.md §10). `AuditLogEntry`
   likewise deliberately does **not** implement `IAuditableEntity`: audit rows are append-only,
   so an `UpdatedAt` on one would describe a write path that must not exist.

## Expected Deliverables

`Models/*.cs` — 19 files (13 entities, 5 enums, `IAuditableEntity`).

## Dependencies

§4 (token design dictates `Session`/`RefreshToken`/`SigningKey` shapes). Blocks §7.

## Security Considerations

Property names make storage form explicit (`PasswordHash`, `TokenHash`, `SecretEncrypted`, `PrivateKeyProtected`) — a reviewer can spot a plaintext-secret field by naming convention alone. `User.PasswordHash` is nullable: social/passkey-only accounts must not have a fake password.

## Testing Requirements

Covered via §7 (mapping round-trips) and §20.

## Documentation Requirements

Entity table in this roadmap kept in sync; mermaid ER diagram updated on schema change.

## Definition of Done

18 files compile; every mandated modularity rule satisfied (one class per file); ER diagram matches code.

**Status:** 19 files compile clean (0 warnings under `TreatWarningsAsErrors`); one type per file;
`00-overview.md` entity table and ER diagram updated to match. Open item: owner sign-off on the
three deviations above.

## Questions for the Project Owner

1. `User.DisplayName` is the only mutable profile field, so `PATCH /users/me` has something to
   write. Is a single free-text name the intended v1 profile surface, or should it carry more
   (locale, timezone, avatar URL)? Anything added later is a migration, not a redesign.
