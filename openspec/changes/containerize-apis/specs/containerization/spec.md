## Purpose

Defines the container deliverable and local container orchestration for the two APIs: production images built from official .NET 9 runtimes that run non-root and bind all interfaces, and a `docker compose` stack that boots both APIs and seeds the shared credential store in one command.

## ADDED Requirements

### Requirement: Production container images exist for both APIs

The repository SHALL provide a production `Dockerfile` for each API (CmsWebhook and Users), each building the Release publish output on the official .NET 9 SDK image and running it on the official .NET 9 ASP.NET runtime image as a non-root user. Each image SHALL bind Kestrel to `http://0.0.0.0:8080` so the API is reachable from outside the container (the container analogue of the production binding requirement).

#### Scenario: Image builds from a clean checkout

- **WHEN** a Dockerfile is built from the repository root with `docker build`
- **THEN** the build succeeds and produces a runtime image whose default entrypoint starts the API

#### Scenario: API inside a container binds all interfaces

- **WHEN** the image runs a container without a host-port override
- **THEN** the API listens on `0.0.0.0:8080` inside the container, reachable via a published host port

#### Scenario: Runtime runs as a non-root user

- **WHEN** the container is started
- **THEN** the API process runs as a non-root user, so a container escape cannot run with root privileges

### Requirement: One command starts the full local stack

The repository SHALL ship a `docker-compose.yml` that starts both APIs and initializes the credential store with one command. The `init` service SHALL seed the shared credential store (via the existing initialization tooling, using the local-development default passwords) and complete successfully before either API starts; both APIs SHALL share one volume holding the SQLite stores; the CMS Webhook API SHALL be reachable on host port `5264` and the Users API on host port `5265`, so the README's existing curl walkthrough works unchanged.

#### Scenario: Starting the stack

- **WHEN** `docker compose up` completes
- **THEN** the credential store is seeded, the CMS Webhook API answers on port `5264` and the Users API on port `5265`, and both `/health` probes return healthy without credentials

#### Scenario: The walkthrough works against the stack

- **WHEN** the README curl sequence (CMS publish event, regular-user listing, administrator disable/enable) is run against the composed stack
- **THEN** each request returns the documented status code, proving both APIs read and write the same shared stores

### Requirement: Devcontainer forwards the API ports

The devcontainer configuration SHALL forward the ports the APIs actually bind — `5264` and `5265` — so an API started inside the devcontainer is reachable from the host browser.

#### Scenario: Host reaches an API started in the devcontainer

- **WHEN** the CMS Webhook API runs inside the devcontainer on port `5264`
- **THEN** `http://localhost:5264/health` from the host machine returns healthy
