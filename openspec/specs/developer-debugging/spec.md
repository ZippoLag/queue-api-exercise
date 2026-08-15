# Developer Debugging Specification

> Source files: .vscode/launch.json, .vscode/tasks.json, docker-compose.dev.yml, README.md, docs/development-style.md, docs/configuration.md

## Purpose

Defines the developer debugging experience for the two APIs: a documented debugging workflow covering the host, the devcontainer, and the container surfaces; an explicit container debugging mode that runs both APIs from source with hot reload against the same stores the host development surface uses (the CmsWebhook project's `db/` folder) without altering the default `docker compose up`; and VS Code wiring (tasks and launch profiles) to orchestrate the stack and attach a debugger to container processes.

## Requirements

### Requirement: Documented debugging workflow for both APIs

The repository SHALL document a debugging workflow that lets a developer attach a debugger to each API (CmsWebhook on port `5264`, Users on port `5265`), covering the host (`dotnet run`/F5), the devcontainer, and the composed-container surfaces, and stating when each is appropriate. The documentation SHALL warn that the composed stack and a host launch use the same host ports and therefore must not run at the same time, SHALL state that the host and debug-container surfaces share the stores under the CmsWebhook project's `db/` directory, and SHALL state that the production-image stack uses its own `queue-db` volume.

#### Scenario: Host debugging from the documentation

- **WHEN** a developer follows the documented debugging workflow and starts both APIs from the host in the Development environment
- **THEN** both APIs are debuggable (breakpoints bind, hot reload applies) and reachable at `5264`/`5265` with the shared local stores

#### Scenario: Container debugging is reachable from the documentation

- **WHEN** a developer follows the documented debugging workflow to debug inside containers
- **THEN** the documented command starts the composed stack in debug mode and the developer can attach a debugger or rely on hot reload

#### Scenario: Mode-mixing hazards are documented

- **WHEN** the documentation describes running the stack in more than one mode
- **THEN** it states the host-port collision (both the stack and host launches bind `5264`/`5265`) and that the production-image stack keeps its isolated `queue-db` volume while the host and debug-container surfaces share the CmsWebhook project's `db/` folder

### Requirement: Container debugging mode without altering the default stack

The repository SHALL provide an explicit, opt-in container debugging mode that runs both APIs from source with hot reload enabled against the same credential/entity stores the host development surface uses — the CmsWebhook project's `db/` folder, bind-mounted into the debug containers — and on the same host ports as the production-image stack, so entities written by a host F5 session are visible in the debug stack and vice versa. Running `docker compose up` without the debug mode SHALL keep building and running the production Release images, which SHALL keep their own isolated `queue-db` volume.

#### Scenario: Debug mode boots both APIs from source against the shared stores

- **WHEN** the documented debug-mode command is run
- **THEN** both APIs start from source with hot reload enabled, the credential store is seeded in the host `db/` folder, both APIs are reachable on the same host ports as the production-image stack, and entities written by a host run are visible in the debug stack (and vice versa)

#### Scenario: Default stack is unchanged

- **WHEN** `docker compose up` is run without the debug mode
- **THEN** the production images are built and run as specified by the containerization capability, with no debug-only behavior and with the stores in the `queue-db` volume rather than the host `db/` folder

### Requirement: VS Code orchestration and attach

The repository SHALL ship VS Code task definitions to start and stop the composed stack (both the default and the debug mode) and launch configurations to launch both APIs on the host and to attach a debugger to an API process running in a debug-mode container.

#### Scenario: Editor tasks orchestrate the stack

- **WHEN** a developer runs the compose tasks from the editor
- **THEN** the stack is started, stopped, or reset exactly as the documented CLI commands do

#### Scenario: Editor attaches to a container process

- **WHEN** an API runs inside a debug-mode container
- **THEN** the developer can attach a debugger to that process from the editor
