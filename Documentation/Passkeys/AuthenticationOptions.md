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
Optional account hint used only to choose allow-credentials behavior.
## Validation rules
Any hint must be a structurally valid bounded email address.
## Success response
`200 OK` with challenge and assertion options.
## Error responses
`400 validation_failed`; `429 rate_limited`.
## Example request
```http
POST /api/v1/passkeys/authentication/options
Content-Type: application/json

{"email":"user@example.com"}
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"challenge":"<base64url>","rpId":"example.com"}
```
## Security considerations
Existing and absent account hints must produce indistinguishable responses; otherwise this becomes an account-enumeration endpoint.
## Related endpoints
[`Complete passkey authentication`](AuthenticationComplete.md), [`Login`](../Auth/Login.md).
