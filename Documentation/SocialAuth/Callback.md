---
method: GET
route: /api/v1/auth/social/{provider}/callback
auth: anonymous
---

# Complete social authorization

## Purpose
Validates the provider callback, links or creates the external identity, and starts a session.
## HTTP method
`GET`
## Route
`/api/v1/auth/social/{provider}/callback`
## Authentication requirements
Anonymous; signed state plus provider code are the flow credentials.
## Authorization requirements
None.
## Request headers
Optional `X-Auth-Transport` if the client controls callback transport selection.
## Route parameters
`provider`: the provider used to start the flow.
## Query parameters
Provider `code`, signed `state`, and provider error fields.
## Request body
None.
## Validation rules
Provider, code, and state must be present, bounded, and mutually consistent.
## Success response
`200 OK` with login tokens or cookie-mode acknowledgement.
## Error responses
`400 validation_failed`; `404 not_found`; `429 rate_limited`.
## Example request
```http
GET /api/v1/auth/social/google/callback?code=<code>&state=<state>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"accessToken":"<access-token>","refreshToken":"<refresh-token>","expiresIn":900}
```
## Security considerations
Accounts are matched by provider plus provider-account ID, never email alone. Provider email verification semantics must be checked explicitly.
## Related endpoints
[`Start social authorization`](Authorize.md), [`List linked accounts`](../Users/ListLinkedAccounts.md).
