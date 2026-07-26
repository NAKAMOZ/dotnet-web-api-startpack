---
method: DELETE
route: /api/v1/api-keys/{keyId}
auth: required
---

# Revoke API key

## Purpose
Permanently revokes one API key owned by the caller.
## HTTP method
`DELETE`
## Route
`/api/v1/api-keys/{keyId}`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone; ownership is enforced in the update predicate.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
`keyId`: API-key GUID.
## Query parameters
None.
## Request body
None.
## Validation rules
The key must exist for the caller and not already be revoked.
## Success response
`204 No Content`.
## Error responses
`401 unauthorized`; `403 csrf_validation_failed`; `404 not_found`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/api-keys/01900000-0000-7000-8000-000000000003
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
Revocation is immediate for subsequent requests. Another user's key ID is indistinguishable from an unknown ID.
## Related endpoints
[`List API keys`](List.md), [`Create API key`](Create.md).
