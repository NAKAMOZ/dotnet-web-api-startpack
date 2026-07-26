---
method: GET
route: /api/v1/admin/users
auth: required
---

# Admin list users

## Purpose
Returns a filterable, paged administrative user list.
## HTTP method
`GET`
## Route
`/api/v1/admin/users`
## Authentication requirements
Bearer JWT, access cookie, or API key.
## Authorization requirements
Permission `users:read:any`.
## Request headers
One supported authentication transport.
## Route parameters
None.
## Query parameters
Page, page size, search/filter fields, sort field, and direction.
## Request body
None.
## Validation rules
Pagination is bounded and sorting is restricted to an allow-list.
## Success response
`200 OK` with items and paging metadata.
## Error responses
`400 validation_failed`; `401 unauthorized`; `403 forbidden`; `429 rate_limited`.
## Example request
```http
GET /api/v1/admin/users?page=1&pageSize=25&sortBy=createdAt
Authorization: Bearer <admin-token>
```
## Example response
```http
HTTP/1.1 200 OK
Content-Type: application/json

{"items":[],"page":1,"pageSize":25,"totalCount":0}
```
## Security considerations
Search and sort fields are allow-listed to avoid exposing or ordering by internal credential and lockout columns.
## Related endpoints
[`Get user`](GetUser.md), [`Update user`](UpdateUser.md).
