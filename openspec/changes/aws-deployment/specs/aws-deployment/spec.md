## Purpose

Defines the AWS deployment footprint and pipeline for the two APIs: a single node hosting both APIs over one shared SQLite store, TLS termination for Basic Auth, secrets supplied through AWS Systems Manager, and a CI pipeline that deploys `main` and verifies the live deployment.

## ADDED Requirements

### Requirement: Both APIs deploy to a single node over one shared store

The AWS deployment SHALL run both the CmsWebhook API and the Users API on one compute node that owns the two shared SQLite files (credential and CMS stores) on a persistent EBS volume, so the outbox worker's writes and the Users API's reads address the same files as in local development. The node SHALL be sized at the lowest tier that runs the workload (`t4g.micro`), and the deployment SHALL run exactly one replica of each API.

#### Scenario: Stores persist across a redeploy

- **WHEN** the node is redeployed (new artifact applied to the same node)
- **THEN** the SQLite files on the EBS volume are preserved and the previously ingested entities and seeded credentials remain intact

#### Scenario: Scaling out is not part of the footprint

- **WHEN** the deployment footprint is inspected
- **THEN** it declares a single replica per API, with multi-instance scaling documented as out of scope until the shared store is moved off SQLite

### Requirement: Traffic to the APIs is served over TLS

Because authentication is HTTP Basic (base64, not encrypted), all client traffic to both APIs SHALL be served over HTTPS, terminated by an AWS ALB with an ACM certificate. Plain HTTP SHALL redirect to HTTPS. The APIs themselves SHALL listen on plain HTTP on their private ports behind the ALB.

#### Scenario: HTTPS terminates at the load balancer

- **WHEN** a client requests `https://<api-host>/health`
- **THEN** the request succeeds through the ALB, which terminates TLS and forwards plain HTTP to the API

#### Scenario: Plain HTTP redirects

- **WHEN** a client requests `http://<api-host>/health`
- **THEN** the response redirects (301/302) to the HTTPS URL

#### Scenario: APIs bind all interfaces

- **WHEN** the APIs start on the node
- **THEN** each binds `http://0.0.0.0:<port>` (not the default loopback-only bind), so the ALB target group can reach them

### Requirement: Load-balancer health checks use the anonymous liveness probe

The ALB target groups SHALL health-check the anonymous `/health` endpoint of each API, which returns `200` when the application is running, so the ALB can route traffic without credentials and can drain an unhealthy API.

#### Scenario: Healthy API passes the probe

- **WHEN** an API is running and its store is reachable
- **THEN** the target-group health check against `/health` returns `200` and the target is marked healthy

### Requirement: Secrets are supplied by AWS Systems Manager

The three user passwords and both connection strings SHALL be stored as `SecureString` parameters in SSM Parameter Store and injected into the API processes as environment variables at boot. The committed local-development default passwords SHALL never be used by the deployed APIs; deployment SHALL generate fresh random passwords for the seeded users.

#### Scenario: API credentials come from the parameter store

- **WHEN** the node boots an API
- **THEN** the process environment contains the connection strings and passwords read from SSM, not from the repository's committed defaults

#### Scenario: Seeded users authenticate with generated passwords

- **WHEN** a client authenticates against the deployed APIs using the generated credentials
- **THEN** authentication succeeds, and the local-development default passwords from the repository fail to authenticate

### Requirement: CI deploys main and verifies the deployment

The CI workflow SHALL, on a push to `main` that passes the existing build, test, coverage, and end-to-end gates, publish both APIs, deploy the artifacts to the AWS node, and verify the live deployment: both `/health` endpoints healthy and the documented smoke flow passing against the deployed APIs.

#### Scenario: Green main deploys

- **WHEN** a push to `main` passes all existing quality gates
- **THEN** CI publishes and deploys both APIs and the deploy job reports success only after the live verification passes

#### Scenario: Failing gates skip the deploy

- **WHEN** a push to `main` fails any existing quality gate
- **THEN** no deployment is performed
