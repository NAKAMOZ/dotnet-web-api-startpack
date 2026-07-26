---
method: POST
route: /api/v1/passkeys/authentication/options
auth: anonymous
---

# Passkey authentication options

## Purpose
Starts an anonymous WebAuthn assertion ceremony.
## HTTP method
`POST`
## Route
`/api/v1/passkeys/authentication/options`
## Authentication requirements
Anonymous.
## Authorization requirements
None.
## Request headers
`Content-Type: application/json`.
## Route parameters
None.
## Query parameters
None.
## Request body
An empty JSON object. Account hints are intentionally not accepted; only discoverable credentials are supported.
## Validation rules
No fields are accepted.
## Success response
`200 OK` with challenge and assertion options.
## Error responses
`400 validation_failed`; `429 rate_limited`.
## Example request
```http
POST /api/v1/passkeys/authentication/options
Content-Type: application/json

{}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"challenge":"<base64url>","rpId":"example.com"}
```
## Security considerations
`allowCredentials` is always empty, so neither account existence nor authenticator count is disclosed.
## Related endpoints
[`Complete passkey authentication`](AuthenticationComplete.md), [`Login`](../Auth/Login.md).
