---
method: DELETE
route: /api/v1/users/me/accounts/{accountId}
auth: required
---

# Unlink a social account

## Purpose
Removes one external identity from the caller.
## HTTP method
`DELETE`
## Route
`/api/v1/users/me/accounts/{accountId}`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone; ownership is enforced in the delete query.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
`accountId`: linked-account GUID.
## Query parameters
None.
## Request body
None.
## Validation rules
The link must belong to the caller and another usable authentication method must remain.
## Success response
`204 No Content`.
## Error responses
`401 unauthorized`; `403 csrf_validation_failed`; `404 not_found`; `409 conflict`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/users/me/accounts/01900000-0000-7000-8000-000000000004
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
The operation refuses to strand the account with no password, passkey, or remaining provider. Cross-user IDs return 404.
## Related endpoints
[`List linked accounts`](ListLinkedAccounts.md), [`Register passkey`](../Passkeys/RegistrationOptions.md).
