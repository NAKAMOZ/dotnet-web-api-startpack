---
method: GET
route: /api/v1/admin/audit-logs
auth: required
---

# List audit logs

## Purpose
Queries the append-only security event trail.
## HTTP method
`GET`
## Route
`/api/v1/admin/audit-logs`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Permission `audit:read`.
## Request headers
One supported authentication transport.
## Route parameters
None.
## Query parameters
Optional user ID, event type, start/end timestamps, correlation ID, page, and page size.
## Request body
None.
## Validation rules
Date ranges, identifiers, event names, correlation format, and pagination are structurally bounded.
## Success response
`200 OK` with matching audit rows and paging metadata.
## Error responses
`400 validation_failed`; `401 unauthorized`; `403 forbidden`; `429 rate_limited`.
## Example request
```http
GET /api/v1/admin/audit-logs?eventType=LoginFailed&page=1&pageSize=25
Authorization: Bearer <admin-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"items":[],"page":1,"pageSize":25,"totalCount":0}
```
## Security considerations
The endpoint is read-only and highly privileged. Metadata is redacted before storage, but callers must still treat IP, user-agent, and event context as sensitive.
## Related endpoints
[`Get user`](GetUser.md), [audit architecture](../Architecture/AuditTrail.md).
