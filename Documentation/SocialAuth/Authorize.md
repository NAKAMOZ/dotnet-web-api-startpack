---
method: GET
route: /api/v1/auth/social/{provider}/authorize
auth: anonymous
---

# Start social authorization

## Purpose
Creates signed single-use OAuth state and starts Google or GitHub login.
## HTTP method
`GET`
## Route
`/api/v1/auth/social/{provider}/authorize`
## Authentication requirements
Anonymous.
## Authorization requirements
None.
## Request headers
Accept JSON for an authorization URL or follow the provider redirect behavior.
## Route parameters
`provider`: supported configured provider name.
## Query parameters
None.
## Request body
None.
## Validation rules
The provider must be supported and configured.
## Success response
`200 OK` with an authorization URL or `302 Found` to that URL.
## Error responses
`404 not_found`; `429 rate_limited`.
## Example request
```http
GET /api/v1/auth/social/google/authorize
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"authorizationUrl":"https://accounts.example/authorize?..."}
```
## Security considerations
State must be unpredictable, integrity-protected, short-lived, and single-use to prevent login CSRF and callback substitution.
## Related endpoints
[`Social callback`](Callback.md), [`Login`](../Auth/Login.md).
