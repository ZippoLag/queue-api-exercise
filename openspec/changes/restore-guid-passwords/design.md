## Context

See proposal.md - Why. The GUID password rule from the initial requirements (`Password: random guid.`) was lost when the Basic-auth delta spec was synced to main: the current `cms-webhook-api` spec dropped the GUID sentence, no test enforces it, and the AWS tooling generates 32-char hex (`openssl rand -hex 16`) instead of GUIDs — so the deployed demo credentials are not GUIDs. The delta specs (`cms-webhook-api` "Configured credential format", `aws-deployment` "Secrets are supplied by AWS Systems Manager") now require: seeded passwords are randomly generated GUIDs, the initialization script rejects non-GUID passwords, and AWS-generated/rotated passwords are GUIDs.

## Goals / Non-Goals

**Goals:**
- Re-establish the GUID rule at the operator-facing boundary: the init tool refuses non-GUID passwords.
- Make both AWS password sources (bootstrap generation and the rotation runbook) produce GUIDs.
- Add a test that locks the rule in so it cannot silently drift again.
- Align the docs (configuration + deployment runbook) with the rule.

**Non-Goals:**
- No change to the local-development default passwords (already GUIDs).
- No change to `AuthDbInitializer.InitializeAsync`'s seeding contract (it stays a permissive library that hashes any supplied password; see D1).
- No runtime authentication change — the APIs verify against stored hashes as today.
- No change to how the deployed node is provisioned beyond rotating its stored credentials to GUIDs.

## Decisions

### D1: Enforce the GUID rule in `Cli` (the operator-facing front-end), not in `AuthDbInitializer`

The spec's "initialization script" is the CLI (`tools/AuthDbInit/Cli.RunAsync`), which `scripts/init-db.sh` and the AWS deploy invoke. Validation lives there, checking each of the three positional passwords with `Guid.TryParseExact(password, "D", out _)` and failing with a descriptive error + exit code 1 when any is not a GUID in the dashed 8-4-4-4-12 format.

**Why `TryParseExact "D"` and not `Guid.TryParse`:** `Guid.TryParse` also accepts the 32-char N format (`131ca3ba...`), which is exactly the non-GUID hex the AWS tooling produced — it would not reject the current drift. The "D" format requires the dashes, matching the local defaults and the initial-requirements intent.

**Why the CLI and not the library:** `AuthDbInitializer.InitializeAsync` is a reusable seeding library, and its existing test `InitializeAsync_WhenRunWithDifferentPasswords_LeavesExistingUsersUnchanged` deliberately seeds non-GUID passwords ("another-cms-password") to prove idempotence over an existing store. Putting validation in the library would break that legitimate contract; the operator-facing CLI is the right enforcement point, and the AWS tooling passes through it.

### D2: Generate GUIDs with `openssl rand` + formatting, reusing the existing dependency

`scripts/bootstrap-aws.sh` and the rotation runbook currently use `openssl rand -hex 16` (already a documented dependency of the bootstrap). Replace it with a GUID-shaped value derived the same way: `hex="$(openssl rand -hex 16)"` then emit `${hex:0:8}-${hex:8:4}-${hex:12:4}-${hex:16:4}-${hex:20:12}`. No new tooling assumption (`uuidgen` is not guaranteed everywhere the script/doc runs — e.g. Git Bash on Windows), no external service.

### D3: Keep docs and tooling consistent in one change

`docs/configuration.md` (credential store section: passwords must be dashed GUIDs; the init tool rejects others) and `docs/deployment-aws.md` (rotation command produces a GUID) are updated in the same change as the code, per the one-fact-one-home documentation convention.

## Risks / Trade-offs

- [Rotating the live demo credentials invalidates the previously posted hex passwords] → The proposal declares this **BREAKING** and the user has approved it; new GUID credentials are reported after rotation.
- [An existing store seeded with hex passwords becomes "wrong" by the new rule] → The rule governs new seeds and generation; the rotation task re-seeds the demo store with GUIDs. Local dev stores were already seeded with GUIDs.
- [`TryParseExact "D"` rejects uppercase-with-dashes? No — it is case-insensitive] → Both `uuidgen`-style lowercase and uppercase GUIDs parse; no issue.
- [Docs and tooling drift apart again] → The added test only covers the CLI; the bootstrap/rotation generation is verified by the rotation task's live check and documented in one place per D3.

## Migration Plan

1. Implement CLI validation + tests (code).
2. Update bootstrap + rotation runbook generation (tooling/docs).
3. Rotate the deployed `demo` credentials: write three GUIDs to SSM (`put-parameter --overwrite`), delete the node store, redeploy (`scripts/deploy-aws.sh` re-seeds from SSM and restarts the services), then verify the smoke flow with the new GUIDs.
4. Rollback is the documented rotation inverse: restore the previous SSM values and redeploy.

## Open Questions

None — the deployment details (region `eu-west-3`, env `demo`, SSM paths `/queue-api/demo/*-password`) are already documented and were verified during exploration.
