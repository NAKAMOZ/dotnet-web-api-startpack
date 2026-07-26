---
method: POST
route: /api/v1/admin/users/{userId}/roles
auth: required
---

# Grant role

## Purpose
Assigns one role to a user.
## HTTP method
`POST`
## Route
`/api/v1/admin/users/{userId}/roles`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Permission `roles:assign`.
## Request headers
`Content-Type: application/json`; cookie mode requires CSRF.
## Route parameters
`userId`: target user GUID.
## Query parameters
None.
## Request body
`roleId`.
## Validation rules
Both user and role must exist and the assignment must not already exist.
## Success response
`204 No Content`.
## Error responses
`400 validation_failed`; `401 unauthorized`; `403 forbidden` or `csrf_validation_failed`; `404 not_found`; `409 conflict`; `429 rate_limited`.
## Example request
```http
POST /api/v1/admin/users/01900000-0000-7000-8000-000000000005/roles
Authorization: Bearer <admin-token>
Content-Type: application/json

{"roleId":"01900000-0000-7000-8000-000000000006"}
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
The grant is audit-recorded with target and actor. Existing access tokens retain old roles until their short expiry.
## Related endpoints
[`Revoke role`](RevokeRole.md), [`Get user`](GetUser.md).
