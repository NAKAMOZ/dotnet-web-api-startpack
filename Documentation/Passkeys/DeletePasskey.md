---
method: DELETE
route: /api/v1/passkeys/{credentialId}
auth: required
---

# Delete a passkey

## Purpose
Removes one credential owned by the caller.
## HTTP method
`DELETE`
## Route
`/api/v1/passkeys/{credentialId}`
## Authentication requirements
Bearer JWT or access cookie.
## Authorization requirements
Authentication alone; ownership is part of the delete predicate.
## Request headers
Cookie mode requires a valid CSRF header.
## Route parameters
`credentialId`: bounded WebAuthn credential identifier.
## Query parameters
None.
## Request body
None.
## Validation rules
The identifier must exist for the caller.
## Success response
`204 No Content`.
## Error responses
`401 unauthorized`; `403 csrf_validation_failed`; `404 not_found`; `429 rate_limited`.
## Example request
```http
DELETE /api/v1/passkeys/AbCdEf
Authorization: Bearer <access-token>
```
## Example response
```http
HTTP/1.1 204 No Content
```
## Security considerations
Unknown and cross-user credential IDs both return 404. Clients should avoid removing their last usable login method accidentally.
## Related endpoints
[`List passkeys`](ListPasskeys.md), [`Register passkey`](RegistrationOptions.md).
