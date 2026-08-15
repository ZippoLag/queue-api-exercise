## ADDED Requirements

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
