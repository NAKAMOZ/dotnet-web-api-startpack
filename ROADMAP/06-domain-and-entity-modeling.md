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

- [ ] One file per entity in `Models/` exactly as listed in the entity table (13 files).
- [ ] `Models/Enums/` — one file per enum (4 files).
- [ ] `Models/IAuditableEntity.cs`.
- [ ] XML doc comments on every security-relevant property (what is hashed, what is plaintext, why).

## Expected Deliverables

`Models/*.cs` (18 files total).

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

## Questions for the Project Owner

None.
