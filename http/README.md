# HTTP request files

Hand-driven requests for local development, one file per controller — the same split as
[`Documentation/`](../Documentation/) and the controller list in §11.

The template's single `dotnet-web-api-startpack.http` was deleted in §3 along with the
`/weatherforecast` sample. Every inventory endpoint now has a request here. Feature-service
stubs currently answer 501; the same requests become executable as each §12 slice lands.

## Layout

```text
http/
  http-client.env.json          shared variables (host, ports) — committed
  http-client.private.env.json  tokens and secrets — gitignored, never committed
  Auth.http                     register, login, login/mfa, refresh, logout, csrf
  SocialAuth.http               authorize, callback
  Sessions.http                 list, revoke one, revoke all
  EmailVerification.http        send, confirm
  PasswordReset.http            request, confirm
  Mfa.http                      totp enroll/confirm/disable, recovery codes
  Passkeys.http                 registration + authentication ceremonies, list, delete
  ApiKeys.http                  create, list, revoke
  Users.http                    me, update, delete, change password, linked accounts
  Admin.http                    users, roles, sessions, audit logs
  WellKnown.http                jwks
```

## Conventions

- **Never commit a real token.** Access tokens, refresh tokens, API keys and passwords
  belong in `http-client.private.env.json`, which is gitignored. A token pasted into a
  committed `.http` file is a credential leak with a permanent history.
- Reference variables as `{{host}}`, `{{accessToken}}` — never inline literals.
- Requests that depend on earlier state (a session, a verification token) say so in a
  comment naming the request that produces it.

## Running them

VS Code with the REST Client extension, Visual Studio, or JetBrains Rider all execute
`.http` files natively. Start the API first:

```bash
dotnet run
```

Create `http-client.private.env.json` beside the shared environment file for credentials:

```json
{
  "dev": {
    "accessToken": "",
    "adminAccessToken": "",
    "refreshToken": "",
    "csrfToken": "",
    "verificationToken": "",
    "passwordResetToken": "",
    "mfaTicket": "",
    "totpCode": "",
    "apiKey": "",
    "socialCode": "",
    "socialState": "",
    "passkeyChallengeId": "",
    "passkeyRegistrationCredential": "",
    "passkeyAuthenticationCredential": ""
  }
}
```
