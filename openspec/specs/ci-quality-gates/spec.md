# CI Quality Gates Specification

> Source files: .config/coverage-min.txt, Directory.Build.props, global.json, scripts/check-coverage.sh, scripts/smoke-e2e.sh, tests/E2E/QueueApi.E2E.Tests/EndToEndSmokeTests.cs, .github/workflows/ci.yml

## Purpose

The repository's automatic quality gates: every push and pull request is verified by `.github/workflows/ci.yml`, which builds with warnings treated as errors, runs the full test suite with coverage collection, enforces a coverage ratchet that never regresses, and validates the OpenSpec specs. These gates are machine-enforced so quality checks do not depend on developer memory.

## Requirements

### Requirement: The .NET SDK family is pinned

The build SHALL use the .NET SDK family pinned in `global.json` (`9.0`, `rollForward: latestFeature`), so the same toolchain version range is used locally and in CI.

#### Scenario: Building with the pinned SDK

- **WHEN** `dotnet` is invoked from the repository root
- **THEN** the SDK resolution honors the pinned family and rolls forward only within it

### Requirement: Compiler and analyzer warnings fail the build

The build SHALL treat all Roslyn compiler and analyzer warnings as errors (`TreatWarningsAsErrors=true` in the root `Directory.Build.props`), so warnings block the build both locally and in CI instead of being deferred.

#### Scenario: Introducing a warning

- **WHEN** a change introduces a compiler or analyzer warning
- **THEN** the build fails and the warning must be fixed or deliberately suppressed

### Requirement: Coverage never regresses below the committed threshold

The coverage gate SHALL measure unique source lines — each line counted once, covered if any test project covers it (union across all cobertura reports, paths normalized) — and SHALL fail when the rate drops below the committed threshold in `.config/coverage-min.txt`. The threshold is a ratchet that only rises; it is currently `100.0`, so any line added without a test fails the gate. The path-normalization map in `scripts/check-coverage.sh` SHALL cover every instrumented source path — including shared projects such as `QueueApi.Persistence` — and SHALL fail loudly when a report emits a path the map does not recognize, so an unseen prefix is surfaced instead of silently mis-aggregated.

#### Scenario: Coverage regression

- **WHEN** the measured unique-line rate is below the committed threshold
- **THEN** the gate fails with the measured rate, the threshold, and guidance on how to raise it deliberately

#### Scenario: Shared code counted once

- **WHEN** a source line is covered by at least one test project
- **THEN** the line counts as covered exactly once, regardless of how many test projects reference its assembly

#### Scenario: Deliberately raising the threshold

- **WHEN** coverage improves and an operator raises the threshold to at most the newly measured rate
- **THEN** the gate continues to enforce the higher bar

#### Scenario: Unseen path prefix fails loudly

- **WHEN** a coverage report emits a source path the normalization map does not recognize
- **THEN** the gate fails, listing the offending path, instead of silently mis-aggregating it

### Requirement: Spec discipline is validated in CI

The CI workflow SHALL run `openspec validate --all` so spec artifacts stay well-formed and complete.

#### Scenario: Invalid spec artifact

- **WHEN** an OpenSpec artifact is malformed or incomplete
- **THEN** CI reports the validation failure and the push/PR is not green

### Requirement: End-to-end smoke gates cover the documented contract

The CI workflow SHALL run an end-to-end smoke gate that exercises both APIs over one shared store in two layers — an in-process test host (`tests/E2E/QueueApi.E2E.Tests`) and real published processes over real SQLite files (`scripts/smoke-e2e.sh`) — and the gate SHALL fail when either layer fails. The smoke vertical SHALL cover the documented contract of both APIs: the acceptance path (ingest → outbox processing → listing → administrator visibility control) and every deterministic rejection path — `401` for a request without credentials, `400` for a timestamp that is not an ISO 8601 / RFC 3339 date-time, `400` for a payload that is not a JSON object, `400` for an empty or whitespace-only route id, `403` for valid credentials of a user not authorized on the API, and `404` for an unknown entity id. The real-process smoke script SHALL also assert that the Users API serves the browser UI shell at its origin root (per the users-api spec), so the smoke vertical proves the served UI — not just the API endpoints — is reachable in the published-process layer. A rejected request SHALL be shown to record nothing: its unique entity id never appears on the Users API listing. A change that adds or modifies a documented status code or request/response contract of either API SHALL extend this smoke vertical in the same change. Timing-sensitive behaviors — notably the ingestion rate limiter's `429` — SHALL NOT be asserted in the smoke layers; they remain covered by the deterministic API integration suite.

#### Scenario: Smoke gates run on every push

- **WHEN** a push or pull request reaches the `end-to-end` CI job
- **THEN** both layers run — the in-process E2E project and the real-process smoke script — and the job fails when either layer fails

#### Scenario: Rejection paths are asserted

- **WHEN** the smoke vertical sends a non-RFC 3339 timestamp or a non-object payload to the ingestion endpoint, an anonymous request to a protected endpoint, or an empty or whitespace-only id to the enable/disable endpoints
- **THEN** the responses are the documented `401`/`400` statuses and the rejected unique entity id never appears on the Users API listing

#### Scenario: UI shell is served in the smoke vertical

- **WHEN** the real-process smoke script runs against the published APIs
- **THEN** it asserts that the Users API origin root returns the browser UI shell

#### Scenario: Contract changes extend the vertical

- **WHEN** a change adds or modifies a documented status code or request/response contract of either API
- **THEN** the change's tasks include extending the smoke vertical with assertions for the new or changed behavior

#### Scenario: Rate limiting stays out of the smoke layers

- **WHEN** the smoke vertical is executed
- **THEN** it never asserts on the rate limiter's `429` response, which remains covered by the API integration suite with an overridden permit limit

### Requirement: CI deploys main to AWS

The CI workflow SHALL include a deployment stage that runs on pushes to `main` only after the existing build, test, coverage, spec, and end-to-end gates pass. The stage SHALL publish both APIs, upload the publish output to the S3 artifact bucket, deploy both APIs to the AWS footprint, and SHALL verify the live deployment (health probes and the smoke flow) before reporting success; a failure anywhere in the stage SHALL fail the workflow without leaving the deployment half-applied.

#### Scenario: Deploy runs only after gates pass

- **WHEN** a push to `main` passes all existing quality gates
- **THEN** the deployment stage runs and deploys the new artifacts

#### Scenario: Deploy is skipped when a gate fails

- **WHEN** a push to `main` fails any existing quality gate
- **THEN** the deployment stage does not run

#### Scenario: Published artifacts are retrievable by the console bootstrap

- **WHEN** the deployment stage has published both APIs
- **THEN** the publish output is synced to the versioned S3 artifact bucket before being applied, so the same artifacts are available to the console bootstrap script

#### Scenario: Live verification gates the deploy result

- **WHEN** the deployment stage has applied the artifacts
- **THEN** it probes both `/health` endpoints and runs the smoke flow against the deployed APIs, and the stage fails if any check fails
