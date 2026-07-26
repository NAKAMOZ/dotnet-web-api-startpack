---
method: GET
route: /.well-known/jwks.json
auth: anonymous
---

# JSON Web Key Set

## Purpose
Publishes active and retiring ES256 public signing keys for token verification.
## HTTP method
`GET`
## Route
`/.well-known/jwks.json`
## Authentication requirements
Anonymous.
## Authorization requirements
None.
## Request headers
Optional ordinary HTTP caching headers.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
None.
## Success response
`200 OK` with a JWKS `keys` array.
## Error responses
`429 rate_limited`; `500 internal_error` if key publication fails.
## Example request
```http
GET /.well-known/jwks.json
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"keys":[{"kty":"EC","use":"sig","alg":"ES256","kid":"<key-id>","crv":"P-256","x":"<x>","y":"<y>"}]}
```
## Security considerations
Only public material is exposed. Validators must pin ES256; accepting an algorithm from the JWT header would make publishing the key unsafe.
## Related endpoints
[`Login`](../Auth/Login.md), [`Refresh`](../Auth/Refresh.md).
