## Context

See proposal.md - Why. The smoke vertical exists in two CI-runnable implementations that already mirror one flow — the in-process E2E project (`EndToEndSmokeTests`) and the real-process script (`scripts/smoke-e2e.sh`) — and both assert only the acceptance path. The `request-sanitization` change extended the documented failure contract of both APIs and no smoke layer covered it; the `end-to-end` CI job has no spec requirement of its own, so nothing stated what the vertical must cover. The live AWS deployment verifier (`deploy-aws.sh` `verify_live`) is deliberately out of scope: it keeps its existing health + happy-path smoke flow, and touching a real environment is not part of this change.

## Goals / Non-Goals

**Goals:**
- Every deterministic documented rejection path of both APIs is asserted at both CI-runnable smoke layers (E2E in-process, real-process script), so a contract change that breaks rejection behavior fails CI.
- The vertical's required coverage is spec'd in one place (`ci-quality-gates`) and inventoried in `docs/testing.md`.
- All new assertions are deterministic: fixed status codes, no timing-sensitive checks.

**Non-Goals:**
- No rate-limiter (`429`) assertions in any smoke layer — timing-sensitive by design (fixed window, per instance); `429` stays in the API integration suite where the permit limit is overridden for determinism.
- No new unit or API-integration coverage (unchanged; the sanitization contract is already asserted there).
- No change to the coverage ratchet, the `build-and-test` job, or the live AWS deployment verification (`deploy-aws.sh` `verify_live` stays exactly as it is — health probes + the happy-path smoke flow).

## Decisions

### D1: The vertical's contract is spec'd once; the two implementations mirror it

The `ci-quality-gates` requirement enumerates the exact status-code set the vertical SHALL assert (`401` anonymous, `400` invalid timestamp, `400` non-object payload, `400` whitespace-only id, `403` reserved user on the Users API, `404` unknown id, plus the acceptance flow). Each of the two CI-runnable implementations mirrors that list, and `docs/testing.md` carries a single **status-code × layer inventory table** (one row per asserted status, one column per layer) that is the reviewable anti-drift home.

**Why**: one-fact-one-home — the spec is the single definition of what the vertical covers, the table in `docs/testing.md` is where a reviewer diffs against when a contract change lands, and the two implementations are concrete mirrors.

**Alternatives considered**: a shared bash module or a parameterized single script — rejected: the two implementations use different tech (xunit vs bash + curl) and different hosts (in-process `WebApplicationFactory` vs published binaries); the assertion set is small enough to mirror deliberately across two layers.

### D2: Rejection proofs use unique entity ids and single-shot absence checks

Each rejected ingestion uses a unique entity id (e.g. `smoke-reject-ts-1`); the assertion is `400` **plus** a single listing check that the id is absent. A rejected request never enters the pipeline, so absence is immediate — no polling, no timeout. The padded-id check ingests its **own** entity (e.g. `smoke-padded-1`) so it does not disturb the acceptance flow's final entity state.

**Why**: polling for absence is the flaky pattern to avoid; a unique id cannot collide with acceptance-path entities, so the single-shot check is deterministic, and a dedicated padded-id entity keeps the vertical's assertions order-independent.

### D3: No `429` anywhere in the smoke layers

The rate limiter's fixed window is per-instance and wall-clock based, so a real-process or live assertion would be timing-sensitive (and would have to wait out a window). The integration suite already asserts `429` deterministically by overriding `RateLimiting:PermitLimit` via the test factory. Explicitly out of scope per the user's direction.

### D4: E2E scenarios keep the fresh-environment-per-test pattern

`EndToEndSmokeTests` already constructs a new `E2EEnvironment` per scenario; the new rejection scenarios follow the same pattern, so no cross-scenario state can leak.

### D5: The anonymous `401` assertion is a true no-credentials request

`expect_status` in `scripts/smoke-e2e.sh` SHALL omit the `-u` flag when the user argument is empty, so the anonymous `401` assertion sends no `Authorization` header at all — matching the spec scenario "a request without credentials" — rather than an empty-credentials `-u ":"` header (which happens to 401 as well but for the wrong reason).

**Why**: the assertion should prove the documented behavior, not an adjacent one; the auth handler rejects missing and malformed credentials with the same `401`, so the distinction is only observable by asserting the exact case.

## Risks / Trade-offs

- **The two mirrors can drift** (a status code added to the spec but missed in one implementation). → Mitigation: the spec names the exact set, `docs/testing.md` carries the status-code × layer inventory table, and CI runs both layers on every push; review compares the implementations against the table.
- **`verify_live` stays happy-path-only while the local vertical grows** — the live AWS verification is deliberately out of scope, so a rejection regression could ship to the deployment and go unnoticed by the deploy verifier. → Mitigation: the end-to-end CI job runs both local layers against the real published binaries, so the rejection contract is proven before any deploy is allowed; the deploy verifier's existing health + happy-path flow continues to gate deploys.
- **A future contract change forgets the vertical again** (the process gap that motivated this change). → Mitigation: the spec requirement "Contract changes extend the vertical" makes it a stated gate; the status-code × layer inventory table in `docs/testing.md` is the one-place diff target during review (the coverage ratchet cannot machine-check smoke coverage).

## Migration Plan

1. Add the E2E rejection scenarios (D4 pattern), then the smoke-script assertions (D5) — the assertion set is identical, so implement once and mirror.
2. Update `docs/testing.md` (vertical coverage, the status-code × layer inventory table, and the 429 exclusion) in the same change.
3. No data migration; the smoke flows are read-mostly and use throwaway or unique ids.

## Open Questions

None — decisions that could change the specs or approach (shared module vs. mirrored assertions, absence-check strategy, anonymous-401 semantics, 429 exclusion, live-deploy scope) were resolved above.
