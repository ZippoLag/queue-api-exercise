# CI Quality Gates Specification

> Source files: .config/coverage-min.txt, Directory.Build.props, global.json, scripts/check-coverage.sh, .github/workflows/ci.yml

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

The coverage gate SHALL measure unique source lines — each line counted once, covered if any test project covers it (union across all cobertura reports, paths normalized) — and SHALL fail when the rate drops below the committed threshold in `.config/coverage-min.txt`. The threshold is a ratchet that only rises; it is currently `100.0`, so any line added without a test fails the gate.

#### Scenario: Coverage regression

- **WHEN** the measured unique-line rate is below the committed threshold
- **THEN** the gate fails with the measured rate, the threshold, and guidance on how to raise it deliberately

#### Scenario: Shared code counted once

- **WHEN** a source line is covered by at least one test project
- **THEN** the line counts as covered exactly once, regardless of how many test projects reference its assembly

#### Scenario: Deliberately raising the threshold

- **WHEN** coverage improves and an operator raises the threshold to at most the newly measured rate
- **THEN** the gate continues to enforce the higher bar

### Requirement: Spec discipline is validated in CI

The CI workflow SHALL run `openspec validate --all` so spec artifacts stay well-formed and complete.

#### Scenario: Invalid spec artifact

- **WHEN** an OpenSpec artifact is malformed or incomplete
- **THEN** CI reports the validation failure and the push/PR is not green

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
