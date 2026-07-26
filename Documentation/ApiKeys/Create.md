---
method: POST
route: /api/v1/api-keys
auth: required
---

# Create API key

## Purpose
Creates a personal programmatic credential and returns its secret once.
## HTTP method
`POST`
## Route
`/api/v1/api-keys`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Requested scopes cannot exceed the caller's current role-granted permissions.
## Request headers
`Content-Type: application/json`; cookie mode requires CSRF.
## Route parameters
None.
## Query parameters
None.
## Request body
Name, requested scopes, and optional expiry.
## Validation rules
Name, scope count/values, and future expiry are bounded and structurally validated.
## Success response
`201 Created` with metadata and the show-once `ak_...` key.
## Error responses
`400 validation_failed`; `401 unauthorized`; `403 forbidden` or `csrf_validation_failed`; `429 rate_limited`.
## Example request
```http
POST /api/v1/api-keys
Authorization: Bearer <access-token>
Content-Type: application/json

{"name":"CI","scopes":["users:read:self"]}
```
## Example response
```http
HTTP/1.1 201 Created
Content-Type: application/json

{"id":"01900000-0000-7000-8000-000000000003","key":"ak_prefix_<show-once-secret>","name":"CI"}
```
## Security considerations
The secret is returned once, logged nowhere, and stored only as a hash. Effective permission is re-intersected with the owner's roles on each request.
## Related endpoints
[`List API keys`](List.md), [`Revoke API key`](Revoke.md).
