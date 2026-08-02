# ADR-0031: Scalar and OpenAPI exposed only in development and staging

- **Status:** Accepted
- **Date:** 2026-08-02
- **Deciders:** Project owner, through the explicit implementation directive
- **Source:** Resolves **P16**
- **Affects:** §18, §22, §27

## Context

Interactive documentation is valuable during development and staging DAST, but production
does not need to publish a machine-readable inventory of every authentication operation and
schema.

## Decision

Map `/openapi/{version}.json` and `/scalar/{version}` only in Development and Staging. Do not
map either endpoint in Production. Staging uses the OpenAPI document as the ZAP API-scan
target and must be protected by the staging environment's access controls.

## Alternatives considered

- Public production documentation: rejected because clients already have versioned Markdown
  and it expands production reconnaissance surface without runtime benefit.
- Development only: rejected because staging contract inspection and automated DAST need the
  deployed document.
- Authentication around production Scalar: rejected as extra authorization surface for a
  capability production does not require.

## Consequences

- Production exposure remains an executable integration-test invariant.
- A public developer portal, if later required, should publish a reviewed static contract
  independently of the production API process.
