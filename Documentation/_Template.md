---
method: GET
route: /api/v1/example
auth: required
---

# Endpoint title

## Purpose

What the endpoint does and why a client calls it.

## HTTP method

`GET`

## Route

`/api/v1/example`

## Authentication requirements

State whether the route is anonymous or which authentication transports it accepts.

## Authorization requirements

Name permissions, recent-authentication requirements, or state that authentication alone is sufficient.

## Request headers

List required and optional headers, including transport or CSRF behavior.

## Route parameters

List route parameters or state “None.”

## Query parameters

List query parameters, defaults, and bounds, or state “None.”

## Request body

Describe the JSON body or state “None.”

## Validation rules

List the structural rules enforced before the service runs.

## Success response

Give the status and response shape.

## Error responses

List stable `errorCode` values and relevant statuses.

## Example request

```http
GET /api/v1/example HTTP/1.1
Authorization: Bearer <access-token>
```

## Example response

```http
HTTP/1.1 200 OK
Content-Type: application/json

{}
```

## Security considerations

Explain endpoint-specific threats and controls; do not paste generic boilerplate.

## Related endpoints

Link the surrounding flow.
