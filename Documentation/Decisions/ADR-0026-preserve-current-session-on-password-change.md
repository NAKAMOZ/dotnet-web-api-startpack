# ADR-0026: Preserve the current session on deliberate password change

- **Status:** Accepted
- **Date:** 2026-07-26
- **Deciders:** Project owner
- **Source:** §12 feature-service completion scope, S4
- **Affects:** §4, §12, §19, §21
- **Supersedes:** the password-change revocation rule in ADR-0002; password reset is unchanged

## Context

ADR-0002 grouped deliberate password changes and password-reset recovery together and
revoked every session for both. The implemented self-service flow requires the current
password, so the current session plus that proof is stronger evidence than a reset link.
Destroying the session that performed a deliberate change also strands the client before
it can inspect the sibling-session revocation result.

Password reset has a different threat model: it is normally used after credential loss or
suspected compromise and must continue to revoke every session.

## Decision

A successful authenticated password change rotates `User.SecurityStamp`, copies the new
stamp to the current session, and revokes every other live session with
`PasswordChanged`. The current session remains usable.

A successful password reset rotates the stamp and revokes every session, including any
session held by the caller.

## Alternatives considered

- Revoke every session for both flows: rejected because it discards a recently
  authenticated session even after the caller proved the current password.
- Preserve every session and rely only on the new password: rejected because sibling
  refresh-token chains may already be compromised.
- Avoid rotating the security stamp for deliberate changes: rejected because any sibling
  session missed by revocation would remain refreshable.

## Consequences

- Clients can complete a deliberate password change without an immediate re-login.
- Every sibling session is revoked and its old security-stamp snapshot cannot refresh.
- Tests must assert both halves: the current session survives a change, while reset
  revokes all sessions.
