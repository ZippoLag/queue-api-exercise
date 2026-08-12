## Context

The `add-basic-auth` change delivered a correct, fully-tested Basic Auth implementation (build clean, 27/27 tests). A follow-up review found no correctness bugs but three cleanup items and two security constraints that were only implicit. See proposal.md — Why. The current wiring reads `AUTH_CMS_USERNAME` twice (once directly in `Program.cs` for the policy, once inside `EnvironmentUserCredentialsProvider`), the handler emits a role claim nothing consumes, and a `"cms"` constant suggests an identity that the `[10,20]` username rule makes impossible.

## Goals / Non-Goals

**Goals:**
- Remove dead/misleading code so the shared auth library and wiring say exactly what they do.
- Single source of truth for the configured cms username.
- Make the TLS requirement and the accepted timing side-channels explicit design decisions.
- Keep behavior byte-for-byte identical: `401`/`403`/`200` semantics, challenge headers, startup fail-fast.

**Non-Goals:**
- Enforcing HTTPS in code (`UseHttpsRedirection`/HSTS) — deployment concern; dev runs over http locally.
- Rate limiting or credential lockout — separate feature, explicitly deferred.
- Password hashing before comparison — architecture fixes passwords as random GUIDs.
- Changing the spec: no externally observable behavior changes (hence `skip_specs: true`).

## Decisions

### 1. Delete `ReservedCmsUsername = "cms"` instead of wiring it into the policy
The constant is referenced nowhere (verified) and is actively misleading: the spec authorizes the *configured* cms user, and the architecture's `[10,20]` length rule means a configured username can never literally be `"cms"`. Wiring the constant into the policy would break the feature. **Rationale:** remove the confusion; the spec already says "the configured `cms` user", which matches the implementation.
**Alternatives considered:** using the constant in the policy (rejected — would make valid configured usernames fail authorization); renaming it (rejected — any name still describes a non-existent identity).

### 2. Remove the `AuthenticatedUser` role claim from the handler
Nothing consumes `ClaimTypes.Role`. **Rationale:** keep the principal minimal; a claim that future APIs silently inherit could imply authorization semantics that don't exist.
**Alternatives considered:** keeping it as a convention (rejected — dead claims are how implied privileges creep in).

### 3. Single construction of the credential provider in `Program.cs`
Currently `Program.cs` reads `AUTH_CMS_USERNAME` directly to build the policy (with its own copy of the error message) and separately resolves the provider for fail-fast — two sources of truth for one value. Change: construct `new EnvironmentUserCredentialsProvider()` once, register it as the `IUserCredentialsProvider` singleton, and use its `Username` property in the policy. **Rationale:** one env read, one validation path, one error message; the fail-fast `GetRequiredService` stays as the trigger.
**Alternatives considered:** reading the env var once and passing it in (rejected — splits validation between two places); `BuildServiceProvider` to resolve early (rejected — premature container use).

### 4. TLS is a deployment constraint, not code
Basic Auth transmits credentials as base64, which is *not* encryption. **Decision:** production deployments MUST serve `CmsWebhook.Api` over TLS; the http profile in `launchSettings.json` is for local development only. Not enforced in code this increment.
**Alternatives considered:** `UseHttpsRedirection` + HSTS now (rejected — needs an https port in tests, changes the integration-test HTTP surface, and belongs with a deployment/hosting increment).

### 5. Timing side-channels are accepted and documented
Two known channels: (a) an unknown username short-circuits before the constant-time compare, creating a username-existence timing oracle; (b) the `length != length || !FixedTimeEquals(...)` short-circuit reveals whether the presented password length matches. **Decision:** accepted for the current single-user system; the handler already returns identical `401`s and never leaks passwords. If multi-user support or public exposure arrives, harden by hashing both sides (e.g., SHA-256) before the fixed-time compare.
**Alternatives considered:** hashing now (rejected — no functional benefit for one user, adds a moving part).

### 6. Rate limiting / lockout stays out of scope
Brute-force protection on Basic Auth remains a deferred feature. Noted here so it isn't silently forgotten.

## Risks / Trade-offs

- [Dedup changes `Program.cs` wiring] → Behavior is identical; the existing integration tests (401/403/200 + startup failure) cover the exact paths changed.
- [Deleting a public constant could break external consumers] → `QueueApi.Auth` is internal to this solution; verified zero references.
- [TLS not enforced in code] → Explicitly documented as a deployment requirement (decision 4); tracked for a future hosting increment.

## Migration Plan

Greenfield cleanup: no data migration. Rollback = revert the change; behavior is unchanged, so no consumers are affected.

## Open Questions

None — the deferred items (TLS enforcement, rate limiting) are recorded as non-goals, not unknowns.
