## Why

The initial requirements' Performance section asks for a read-only/writer configuration for the application context and optimized EF read queries. The asynchronous-processing half of that requirement is already spec'd (`cms-webhook-api/event-ingestion`), but the read-side half exists only as prose in `docs/architecture.md` and XML comments — it has no spec-level requirement, so the contract is not verifiable and the CI spec gate (`openspec validate --all`) does not cover it.

## What Changes

- Add a spec requirement to the `users-api` capability capturing the read/write EF separation: `GET /entities` is served from a read-only, non-tracking configuration; the enable/disable commands run on a single-writer tracking configuration.
- Declare that listings return each entity's **full stored payload as a JSON string by design** — payloads are not meant to be edited, so no endpoint accepts payload content and no projection strips them.
- Declare that the read representation intentionally **differs from the recorded event's shape**: the read entity carries state the originating CMS event did not (the maintained update timestamp and the administrator-visibility flag), so read and write are not an exact match.
- Align `docs/architecture.md`'s Performance section with this declaration (the section currently describes the AsNoTracking/single-writer mechanics but not why the shapes differ).
- Add a test asserting the listing query does not hydrate EF's change tracker, verifying the read-only guarantee behaviorally.
- **No production behavior change**: the implementation already satisfies the requirement; this change is spec + documentation + verification only.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `users-api`: adds the requirement "Read and write paths use separated configurations" (read-only/non-tracking listing, single-writer commands, full-payload listings by design, read shape intentionally distinct from the recorded event shape).

## Impact

- **Specs**: `openspec/specs/users-api/spec.md` gains one requirement (after archiving this change).
- **Docs**: `docs/architecture.md` — Performance section gains the shape-difference and full-payload rationale.
- **Tests**: `tests/Users/Users.Infrastructure.Tests/EfEntityQueryRepositoryTests.cs` — a no-tracking assertion.
- **No production code, public API, or dependency changes.**
