## 1. In-process E2E suite

- [x] 1.1 Add a rejection scenario: a non-RFC 3339 timestamp (date-only `2024-01-01`) posted to `/cms/events` returns `400` and its unique entity id never appears on the Users API listing
- [x] 1.2 Add a rejection scenario: a non-object payload posted to `/cms/events` returns `400` and its unique entity id never appears on the Users API listing
- [x] 1.3 Add a scenario: an anonymous request to a protected endpoint (both APIs) returns `401`
- [x] 1.4 Add Users API id-validation scenarios: whitespace-only id → `400`; whitespace-padded id (its own ingested entity) → trimmed lookup `204` and hidden from regular users; unknown id → `404`

## 2. Real-process smoke script

- [x] 2.1 Extend `scripts/smoke-e2e.sh` with the same rejection assertions over real HTTP: `401` anonymous, `400` invalid timestamp, `400` non-object payload, `400` whitespace-only id, `204` padded-id trim (a dedicated ingested entity, hidden from regular users), `404` unknown id — each with a unique entity id and a single-shot absence check (no absence-polling)
- [x] 2.2 Make `expect_status` omit `-u` when the user argument is empty (design D5), so the anonymous `401` assertion sends no `Authorization` header at all
- [x] 2.3 Update the script header comment to describe the rejection-path coverage

## 3. Docs

- [x] 3.1 Update `docs/testing.md`: the end-to-end section describes the rejection-path coverage of the vertical, carries a status-code × layer inventory table (one row per asserted status, one column per layer) as the anti-drift home, and states the explicit `429` exclusion (rate limiting stays in the API integration suite)

## 4. Verification

- [x] 4.1 Run `dotnet test tests/E2E/QueueApi.E2E.Tests/QueueApi.E2E.Tests.csproj` and `bash scripts/smoke-e2e.sh` locally, and `openspec validate --all`
