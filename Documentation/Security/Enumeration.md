# Account Enumeration

Source of truth for what an **anonymous** caller may learn about which accounts exist (§16).

This document is written **ahead of** the services that implement it. §12's feature services do not exist yet, so nothing below is enforced by code today — the parity table is the specification each service is built against, and §22 turns each row into a test. Where a row and an implementation disagree, the row wins until this document is deliberately changed.

---

## 1. The property

> For every anonymous endpoint, the response to a request naming an **existing** account must be indistinguishable from the response to one naming an account that **does not exist**.

Indistinguishable on four axes, all of which have leaked in real systems:

| Axis | What leaks it |
|---|---|
| **Status** | `409` vs `202`; `401` vs `404` |
| **Body** | different `errorCode`, different field set, a list whose length varies |
| **Timing** | one path hashes a password and the other returns immediately |
| **Side effects** | an email that arrives in one case and not the other |

Headers are covered by the same rule — a `Retry-After` present only on the existing-account path is as good a signal as a status code.

## 2. Why this is worth the awkwardness

An enumeration oracle does not compromise an account by itself. It converts an untargeted attack into a targeted one: an attacker with ten million addresses and one common password needs to know which addresses are worth the attempt. It is also the input to credential stuffing, to phishing that names a real service, and — for a service where membership is sensitive at all — to a disclosure that has nothing to do with authentication.

The cost is real and is accepted here: a user who mistypes their address at registration gets a success-shaped response and no account. That is why the registration email is worded as it is (§3 below).

## 3. Per-endpoint parity

Every anonymous (🔓) operation in the inventory. "Exists" and "Absent" describe the account named by the request.

### `POST /api/v1/auth/register`

| | |
|---|---|
| **Absent** | `202`, empty body. Verification email sent. |
| **Exists** | `202`, **byte-identical** body. Email sent to the same address, with different copy: *"someone tried to register an account with this address — you already have one"*, plus a password-reset link. |
| **Timing** | Both paths hash the submitted password. The existing-account path must not skip the Argon2 call as an optimisation; that alone is a ~100 ms oracle. |

> **This resolves the open decision recorded in `Documentation/Errors.md` §4.** The catalogue currently lists `email_already_registered` as `409`. §16 decides `202`: `EmailAlreadyRegisteredException` stays in the codebase and stays mapped, but becomes **internal-only** — like `AccountLockedException`, it is caught on the registration path and converted before it reaches a client. It remains reachable as a `409` only where the caller is already authenticated and already knows the address exists (linking a social account to a taken email).

> The notification email is what keeps this honest. Without it, a silent `202` means a user who genuinely mistyped never learns their account was not created.

### `POST /api/v1/auth/login`

| | |
|---|---|
| **Absent** | `401`, `invalid_credentials` |
| **Wrong password** | `401`, `invalid_credentials` — identical |
| **Locked** | `401`, `invalid_credentials` — identical, and **no `Retry-After`** |
| **No password set** (social- or passkey-only account) | `401`, `invalid_credentials` — identical |
| **Unverified email** | `401`, `invalid_credentials` — identical |
| **Timing** | The unknown-account path verifies the submitted password against a **fixed dummy Argon2 hash** with the same parameters as the real one, and discards the result. Without it, "no user row" returns in a millisecond and "wrong password" in a hundred. |

The dummy hash is computed once at startup, not per request — deriving it per request adds its own measurable cost. It must use `PasswordHashingOptions`, so a parameter change cannot desynchronise the two paths.

`AccountLockedException` exists so the audit trail can record `account_locked` with a reason. It is mapped to the identical problem body as `InvalidCredentialsException` in `ExceptionToProblemDetailsMap`, and `ErrorCatalogTests` fails the build if those two ever diverge.

### `POST /api/v1/auth/login/mfa`

| | |
|---|---|
| **Ticket unknown, expired, consumed, or belonging to another user** | `400`, `invalid_token` — one code for all four |
| **Correct ticket, wrong TOTP code** | `400`, `invalid_token` — identical |

The ticket is opaque and hashed at rest, so it names no account. Nothing on this endpoint may report *which* of the four failed.

### `POST /api/v1/password-reset/request`

| | |
|---|---|
| **Absent** | `202`, empty body. No email. |
| **Exists** | `202`, identical body. Reset email sent. |
| **Exists, no password set** | `202`, identical body. Email sent, worded for an account that signs in socially. |

The asymmetric side effect — an email in one case, none in the other — is invisible to the requester unless they control the mailbox, in which case they own the account. This is the one place where a side-effect difference is acceptable, and it is acceptable *only* because the channel is the account owner's.

Rate limiting here is §17's and is not optional: without it this endpoint is a mail cannon pointed at arbitrary addresses.

### `POST /api/v1/password-reset/confirm`, `POST /api/v1/email-verification/confirm`

| | |
|---|---|
| **Token unknown / expired / already consumed / wrong type** | `400`, `invalid_token` for every case |

Tokens are looked up by hash, so a token that names no row and one that names a consumed row take the same path. Do **not** add "this link has expired, request a new one" as a distinct code: it confirms the token was real, which confirms the address was real.

### `POST /api/v1/passkeys/authentication/options`

The subtlest one on the list, because the leak is in the shape of a success response rather than in an error.

| | |
|---|---|
| **`email` omitted** | `200`, challenge with an empty `allowCredentials` — the discoverable-credential path |
| **`email` names an absent account** | `200`, challenge, `allowCredentials` **empty** |
| **`email` names an account with passkeys** | `200`, challenge, `allowCredentials` **empty** |

`allowCredentials` is empty in every case, so the response carries no account signal at all. Populating it for a known account would disclose both existence *and* the number of registered authenticators. The cost is that non-discoverable credentials are unsupported; `PasskeyAuthenticationOptionsRequest.Email` documents this already, and the honest resolution is to drop the field in §12 rather than accept it and ignore it.

### `POST /api/v1/passkeys/authentication/complete`

| | |
|---|---|
| **Unknown credential id, bad signature, stale or replayed challenge, unknown user** | `401`, `invalid_credentials` for every case |

### `GET /api/v1/auth/social/{provider}/authorize` and `/callback`

| | |
|---|---|
| **Unknown provider** | `400`, `unsupported_provider` — a provider name is public information, not an account |
| **Callback: address already registered to a local account** | Linked and signed in, per Authentication.md §9. No distinguishable response — the caller has already proven control of the address at the identity provider. |
| **Callback: provider asserts an unverified address** | Same response shape as success; the account is created unverified. Never reveals whether the address was already known. |

### `POST /api/v1/auth/refresh`, `GET /api/v1/auth/csrf`, `GET /.well-known/jwks.json`

Not enumeration surfaces — none takes an account identifier. `refresh` presents an opaque token and answers `401` `invalid_credentials` for anything it cannot resolve, including a token whose reuse was detected; `token_reuse_detected` is audited, not returned.

## 4. What deliberately still leaks

Recorded rather than hidden.

- **Authenticated endpoints leak nothing new.** `POST /api/v1/email-verification/send` returns `409` when the address is already verified. The caller is the account owner; there is no oracle.
- **Timing is equalised, not constant.** The dummy-hash path equalises the dominant cost. It does not defend against an attacker with millions of samples and a statistical model; database cache state, TLS session resumption and network jitter all remain. Constant-time responses across a network are not achievable, and the §17 rate limit is what makes the residual difference impractical to sample.
- **Registration still discloses to an authenticated caller** linking a social account to an address that already exists locally, via `409`. Accepted: they hold the identity provider's assertion for that address.
- **A user who mistypes their address at registration gets a success-shaped response.** Mitigated by the notification email, not eliminated.

## 5. Testing (§22)

Each row above is one test. Two rules for writing them:

- **Assert the full body, not the status.** A test that checks `401` twice passes while the two responses carry different `errorCode`s. Compare the serialised problem body.
- **Assert timing as a distribution, not a single pair.** A per-request assertion is flaky on any shared runner. Sample both paths many times and compare medians against a tolerance wide enough for CI noise and narrow enough to catch a skipped hash — an omitted Argon2 call is roughly two orders of magnitude, not a few percent.

## 6. Related

- `Documentation/Errors.md` — the code catalogue; §2 there covers `invalid_credentials` conflation.
- `Documentation/Architecture/Authentication.md` §5 — login flow constraints.
- `Documentation/Security/ASVS-Checklist.md` — V2.2 / V3 traceability.
- `ROADMAP/17-rate-limiting-and-abuse-prevention.md` — the control this document depends on.
