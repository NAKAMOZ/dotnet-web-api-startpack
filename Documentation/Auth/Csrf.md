---
method: GET
route: /api/v1/auth/csrf
auth: anonymous
---

# Get a CSRF token

## Purpose
Issues the readable cookie and token needed for cookie-authenticated state changes.
## HTTP method
`GET`
## Route
`/api/v1/auth/csrf`
## Authentication requirements
Anonymous so a browser can bootstrap cookie mode.
## Authorization requirements
None.
## Request headers
No special headers.
## Route parameters
None.
## Query parameters
None.
## Request body
None.
## Validation rules
None.
## Success response
`200 OK` with the token and a `__Host-` CSRF cookie.
## Error responses
`429 rate_limited`.
## Example request
```http
GET /api/v1/auth/csrf
```
## Example response
```http
HTTP/1.1 200 OK
Set-Cookie: __Host-auth.csrf=<token>; Secure; SameSite=Lax
Content-Type: application/json

{"csrfToken":"<token>"}
```
## Security considerations
The token is bound to a session when one exists and is intentionally readable by same-origin JavaScript; it is not an authentication credential.
## Related endpoints
[`Login`](Login.md), [`Refresh`](Refresh.md), [`Logout`](Logout.md).
