## Purpose

Defines the AWS deployment footprint and pipeline for the two APIs: a single node hosting both APIs over one shared SQLite store, TLS enforced at the node boundary for Basic Auth, secrets supplied through AWS Systems Manager, a copy-pastable console bootstrap script that creates the environment, and a CI pipeline that deploys `main` and verifies the live deployment.

## ADDED Requirements

### Requirement: Both APIs deploy to a single node over one shared store

The AWS deployment SHALL run both the CmsWebhook API and the Users API on one compute node that owns the two shared SQLite files (credential and CMS stores) on a persistent EBS volume, so the outbox worker's writes and the Users API's reads address the same files as in local development. The node SHALL be sized at the lowest-cost tier that runs the workload, preferring free-tier-eligible instance types (`t4g.small`, with a documented downgrade to `t4g.micro` once the free trial ends), and the deployment SHALL run exactly one replica of each API.

#### Scenario: Stores persist across a redeploy

- **WHEN** the node is redeployed (new artifact applied to the same node)
- **THEN** the SQLite files on the EBS volume are preserved and the previously ingested entities and seeded credentials remain intact

#### Scenario: Scaling out is not part of the footprint

- **WHEN** the deployment footprint is inspected
- **THEN** it declares a single replica per API, with multi-instance scaling documented as out of scope until the shared store is moved off SQLite

### Requirement: Traffic to the APIs is served over TLS

Because authentication is HTTP Basic (base64, not encrypted), all client traffic to both APIs SHALL be served over HTTPS, terminated at the node boundary by a reverse proxy on the instance with automatically issued and renewed certificates (e.g. Caddy with Let's Encrypt), or a self-signed internal certificate when no public domain is configured. Plain HTTP SHALL redirect to HTTPS. The APIs themselves SHALL listen on plain HTTP on their private ports behind the proxy.

#### Scenario: HTTPS terminates at the node boundary

- **WHEN** a client requests `https://<api-host>/health`
- **THEN** the request succeeds through the proxy, which terminates TLS and forwards plain HTTP to the API

#### Scenario: Plain HTTP redirects

- **WHEN** a client requests `http://<api-host>/health`
- **THEN** the response redirects (301/302) to the HTTPS URL

#### Scenario: TLS is enforced without a public domain

- **WHEN** no public domain is configured and the client reaches the APIs over the instance's public IP
- **THEN** the proxy still serves HTTPS (self-signed internal certificate) and plain HTTP is rejected or redirected, so Basic Auth credentials never travel unencrypted

#### Scenario: APIs bind all interfaces

- **WHEN** the APIs start on the node
- **THEN** each binds `http://0.0.0.0:<port>` (not the default loopback-only bind), so the proxy can reach them

### Requirement: Live deployment verification uses the anonymous liveness probe

The deployment pipeline SHALL probe the anonymous `/health` endpoint of each API as live verification after applying artifacts — each endpoint returns `200` when the application is running and its store is reachable — so a deploy is reported successful only when both APIs are live.

#### Scenario: Healthy API passes the probe

- **WHEN** an API is running and its store is reachable
- **THEN** the probe against `/health` returns `200`

#### Scenario: Unhealthy API fails the deployment

- **WHEN** either API's `/health` probe does not return `200` after a deploy
- **THEN** the deployment is reported failed and the previous artifacts remain available for rollback

### Requirement: Secrets are supplied by AWS Systems Manager

The three user passwords and both connection strings SHALL be stored as `SecureString` parameters in SSM Parameter Store and injected into the API processes as environment variables at boot. The committed local-development default passwords SHALL never be used by the deployed APIs; the initial creation of the environment SHALL generate fresh random passwords for the seeded users, and re-deploys SHALL reuse the stored credentials.

#### Scenario: API credentials come from the parameter store

- **WHEN** the node boots an API
- **THEN** the process environment contains the connection strings and passwords read from SSM, not from the repository's committed defaults

#### Scenario: Seeded users authenticate with generated passwords

- **WHEN** a client authenticates against the deployed APIs using the generated credentials
- **THEN** authentication succeeds, and the local-development default passwords from the repository fail to authenticate

### Requirement: A copy-pastable console bootstrap script creates the environment

The deployment SHALL provide a single bash script (`scripts/bootstrap-aws.sh`) designed to be pasted into AWS CloudShell that creates the entire environment and performs a first deploy. The script SHALL expose the target region as a single variable at the top, defaulting to `eu-west-3` (Paris), and SHALL be safe to re-run (idempotent). It SHALL clone the repository, install any tooling the console lacks (e.g. Terraform), generate fresh passwords, apply the infrastructure, and deploy artifacts — either the latest artifacts published by CI (downloaded from the S3 artifact bucket) or, when none exist yet, artifacts built locally via an explicit opt-in flag.

#### Scenario: Region is configurable with a Paris default

- **WHEN** an operator opens the script
- **THEN** the region is set by a single variable whose default is `eu-west-3`, and changing the variable before running deploys to the chosen region

#### Scenario: One paste creates and deploys the environment

- **WHEN** an operator pastes and runs the script in CloudShell with CI-published artifacts available
- **THEN** the repository is cloned, the environment is created, and the latest CI-published artifacts are deployed to the node, without the operator installing Terraform or the .NET SDK by hand

#### Scenario: Re-running the script is a no-op on existing infrastructure

- **WHEN** the script is run again against an already-deployed environment
- **THEN** the infrastructure is unchanged (idempotent apply) and the latest artifacts are redeployed

### Requirement: CI deploys main and verifies the deployment

The CI workflow SHALL, on a push to `main` that passes the existing build, test, coverage, and end-to-end gates, publish both APIs, upload the artifacts to the S3 artifact bucket, deploy the artifacts to the AWS node, and verify the live deployment: both `/health` endpoints healthy and the documented smoke flow passing against the deployed APIs.

#### Scenario: Green main deploys

- **WHEN** a push to `main` passes all existing quality gates
- **THEN** CI publishes and deploys both APIs and the deploy job reports success only after the live verification passes

#### Scenario: Failing gates skip the deploy

- **WHEN** a push to `main` fails any existing quality gate
- **THEN** no deployment is performed

### Requirement: CI authenticates to AWS through GitHub OIDC federation

The CI deploy job SHALL authenticate to AWS via GitHub's OIDC federation rather than stored access keys, assuming a deploy role whose trust policy is scoped to this repository and matches GitHub's current OIDC subject-claim format, so a workflow run receives short-lived credentials for exactly that run.

#### Scenario: Deploy job assumes the role without stored keys

- **WHEN** a push to `main` reaches the deploy job
- **THEN** it assumes the deploy role using an OIDC token issued for this repository (audience `sts.amazonaws.com`, subject scoped to this repo), with no AWS access keys stored in GitHub

#### Scenario: Trust policy matches the current subject-claim format

- **WHEN** GitHub issues the OIDC token with its current subject-claim format, including the numeric owner and repository IDs
- **THEN** the trust policy's subject condition matches that format and the role assumption succeeds

### Requirement: Published artifacts match the node's CPU architecture

The deployment pipeline SHALL publish both APIs and the seed tool for the same CPU architecture as the target node (the default node is ARM64/Graviton), so the shipped binaries are executable on that node.

#### Scenario: ARM64 node receives ARM64 artifacts

- **WHEN** the target node is ARM64
- **THEN** the published artifacts are built for `linux-arm64` and start successfully on the node

#### Scenario: x86_64 node receives x86_64 artifacts

- **WHEN** the target node is x86_64
- **THEN** the published artifacts are built for `linux-x64` and start successfully on the node

### Requirement: Node provisioning includes the .NET runtime prerequisites

The node's first-boot provisioning SHALL install the .NET ASP.NET Core runtime together with its system prerequisites, notably the ICU globalization libraries that the runtime requires, so deployed applications start without missing-runtime errors.

#### Scenario: Node boots with complete runtime prerequisites

- **WHEN** a node finishes first-boot provisioning with the .NET runtime installed
- **THEN** the ICU libraries required for globalization are present and both APIs start cleanly

### Requirement: Bootstrap reports the GitHub wiring for a new environment

The bootstrap script SHALL, when it creates a new environment without performing a first deploy (infra-only mode), report the GitHub secrets and variables the CI deploy job needs for that environment — account id, artifact bucket, instance id, environment name, and region — so a new environment can be wired to CI without consulting external documentation.

#### Scenario: Infra-only bootstrap prints the wiring values

- **WHEN** an operator bootstraps a new environment with the infra-only mode
- **THEN** the report lists the exact GitHub secret and variable values to set for the deploy job

#### Scenario: Setting the reported values enables CI deploys to the new environment

- **WHEN** the operator sets the reported secrets and variables in GitHub and pushes to `main`
- **THEN** the deploy job targets the new environment and deploys it
