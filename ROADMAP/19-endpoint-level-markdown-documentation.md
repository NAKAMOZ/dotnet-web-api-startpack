# 19. Endpoint-Level Markdown Documentation

## Objective

One Markdown file per endpoint with the mandated 16 sections, mechanically prevented from drifting out of sync with the implementation.

## Scope

Documentation tree, template, authoring workflow, sync enforcement.

## Architectural Decisions

- Structure mirrors controllers:

```text
Documentation/
  Auth/            Register.md Login.md LoginMfa.md Refresh.md Logout.md Csrf.md
  SocialAuth/      Authorize.md Callback.md
  Sessions/        ListSessions.md RevokeSession.md RevokeAllSessions.md
  EmailVerification/ Send.md Confirm.md
  PasswordReset/   Request.md Confirm.md
  Mfa/             TotpEnroll.md TotpConfirm.md TotpDisable.md RegenerateRecoveryCodes.md
  Passkeys/        RegistrationOptions.md RegistrationComplete.md AuthenticationOptions.md AuthenticationComplete.md ListPasskeys.md DeletePasskey.md
  ApiKeys/         Create.md List.md Revoke.md
  Users/           GetMe.md UpdateMe.md DeleteMe.md ChangePassword.md ListLinkedAccounts.md UnlinkAccount.md
  Admin/           ListUsers.md GetUser.md UpdateUser.md DeleteUser.md GrantRole.md RevokeRole.md RevokeUserSessions.md ListAuditLogs.md
  WellKnown/       Jwks.md
  _Template.md
```

- `_Template.md` fixes the 16 mandated sections in order: Purpose, HTTP method, Route, Authentication requirements, Authorization requirements, Request headers, Route parameters, Query parameters, Request body, Validation rules, Success response, Error responses, Example request, Example response, Security considerations, Related endpoints.
- **Sync strategy (three enforcement layers)** — answering how these files stay aligned with code and Scalar/OpenAPI:
  1. *Process*: an endpoint PR is incomplete without its doc file — Definition-of-Done gate in §11 and reviewed as part of the diff (doc and code travel together).
  2. *Mechanical*: `tests/IntegrationTests/Documentation/DocumentationSyncTests.cs` loads the generated OpenAPI document at test time, enumerates every operation, and asserts a corresponding Markdown file exists (and vice versa — orphan docs fail too). Route, method, and auth-requirement lines inside each file are asserted against the OpenAPI operation, so the load-bearing facts cannot silently rot. Runs in CI (§26).
  3. *Single source direction*: OpenAPI (generated from code) is authoritative for mechanical facts; Markdown adds what OpenAPI can't express (security considerations, examples with narrative, related-endpoint guidance). The test enforces the overlap, humans own the prose.

## Technology Decisions Requiring Approval

None.

## Tasks

- [ ] Write `Documentation/_Template.md`.
- [ ] Author each endpoint file as its feature slice lands (§11 build order) — 43 files total per the tree above.
- [ ] Implement `DocumentationSyncTests.cs` (file↔operation set equality; per-file route/method/auth assertions via a small front-matter block in each doc: `method`, `route`, `auth` keys the test parses).
- [ ] `Documentation/README.md`: how to author, how the sync test works, front-matter spec.

## Expected Deliverables

Full `Documentation/` tree, template, sync test in CI.

## Dependencies

§11, §18 (document to sync against).

## Security Considerations

Each file's Security Considerations section is mandatory content, not boilerplate — reviewers reject copy-paste (checklist item).

## Testing Requirements

The sync test *is* the test; it fails on missing, orphaned, or fact-drifted docs.

## Documentation Requirements

Self-referential — this workstream is documentation.

## Definition of Done

43/43 files exist, pass sync, and have owner-reviewed security sections.

## Questions for the Project Owner

None.
