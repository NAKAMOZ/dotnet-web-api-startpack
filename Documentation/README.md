# Endpoint documentation

Each public API operation has one Markdown file under the feature directory that mirrors its
controller. Copy `_Template.md` when adding an operation and fill all sixteen sections; an
endpoint change is incomplete until its document changes in the same pull request.

The front matter is a machine-readable contract:

```yaml
---
method: POST
route: /api/v1/auth/login
auth: anonymous
---
```

- `method` is the uppercase HTTP method.
- `route` is the concrete versioned route emitted by OpenAPI.
- `auth` is `anonymous` when OpenAPI has no security requirement and `required` otherwise.
  Permission and recent-authentication details belong in the authorization section.

`DocumentationSyncTests` generates `/openapi/v1.json` from the application and compares the
operation set with every front-mattered file. Missing documents, orphaned documents, and
method/route/auth drift fail the test. A second assertion fixes the sixteen headings and their
order. OpenAPI wins for mechanical facts; the Markdown remains the source of truth for
security narrative, examples, and flow guidance.
