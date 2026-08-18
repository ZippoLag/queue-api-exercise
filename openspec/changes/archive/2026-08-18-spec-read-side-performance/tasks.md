## 1. Spec capture

- [x] 1.1 Add the `users-api` delta spec requirement "Read and write paths use separated configurations" with its four scenarios (no-tracking listing, writer-configuration commands, full stored payload returned, read shape differs from recorded event shape)
- [x] 1.2 Run `openspec validate --all` and confirm the change passes spec discipline

## 2. Documentation alignment

- [x] 2.1 Update `docs/architecture.md` §Performance: state that `GET /entities` returns each entity's full stored payload as a JSON string by design (payloads are not meant to be edited), that the read representation intentionally differs from the recorded event's shape — the entity carries the maintained update timestamp and the administrator-visibility flag — and that the outbox worker's pending sweep stays a tracking query by design (design D4): `FindAsync` resolves the already-tracked row without a database round trip, so an `AsNoTracking` sweep in isolation would cost one query per event; note the Option-2 trigger (sweeps with many pending rows → `AsNoTracking` + `ExecuteUpdateAsync`) per the change design. The §Performance text was drafted during exploration; verify it reads correctly and needs no further edits at apply time
- [x] 2.2 Confirm `docs/testing.md` and `docs/dsl_glossary.md` need no change for the new requirement (no new terms introduced; the listing behavior is already documented)

## 3. Verification

- [x] 3.1 Add a test to `tests/Users/Users.Infrastructure.Tests/EfEntityQueryRepositoryTests.cs` asserting `ListPublishedAsync` leaves EF's change tracker empty (verifies the read-only/no-tracking guarantee behaviorally), with XML docs citing spec "Read and write paths use separated configurations"
- [x] 3.2 Run the solution test suite and the coverage ratchet (`dotnet test QueueApi.slnx` + `bash scripts/check-coverage.sh`) and confirm both pass
