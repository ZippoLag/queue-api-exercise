## Why

The initial requirements demand `Password: random guid`, but the rule was dropped when the Basic-auth delta spec was synced to main. Nothing in the current specs, tests, or AWS tooling enforces GUID-shaped passwords, and the AWS tooling actually generates 32-char hex (`openssl rand -hex 16`) instead — so the deployed `demo` credentials are not GUIDs, silently violating the archived requirement.

## What Changes

- **Restore the password-GUID requirement** to the `cms-webhook-api` spec: the seeded password SHALL be a randomly generated GUID (as originally specified in `docs/archived/initial_requirements.md`).
- **Extend the same requirement to the AWS tooling**: `scripts/bootstrap-aws.sh` and the documented password-rotation command SHALL generate proper GUIDs (8-4-4-4-12 with dashes) instead of `openssl rand -hex 16` output.
- **Add a test enforcing the rule** so it cannot drift again (seed passwords must parse as GUIDs; generated AWS passwords must be GUID-shaped).
- **Rotate the deployed `demo` credentials to proper GUIDs** — update the three SSM parameters, delete the node store, redeploy (documented rotation flow). **BREAKING**: the demo passwords previously posted in this chat (hex strings) are invalidated.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `cms-webhook-api`: the "Configured credential format" requirement regains the sentence that the password SHALL be a randomly generated GUID (lost when the delta spec was synced to main).
- `aws-deployment`: freshly generated and rotated deployment passwords SHALL be random GUIDs, not hex strings.

## Impact

- `scripts/bootstrap-aws.sh` — password generation switches from `openssl rand -hex 16` to GUID generation.
- `docs/deployment-aws.md` — rotation runbook command updated to generate GUIDs.
- `docs/configuration.md` — note that deployed (SSM) passwords are random GUIDs; local defaults already are.
- Tests: `tools/AuthDbInit.Tests` (or a new focused test) gains GUID-format assertions.
- Live demo environment (`demo`): three SSM parameters rotated and the node store re-seeded with GUID passwords.
- `openspec/specs/cms-webhook-api/spec.md`, `openspec/specs/aws-deployment/spec.md` — delta specs for the two modified capabilities.
