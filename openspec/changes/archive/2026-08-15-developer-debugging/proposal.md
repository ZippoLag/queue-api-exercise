## Why

The repository ships a one-command container stack and accurate run documentation, but **no debugging story anywhere**: `.vscode/` contains only theme colors (no `launch.json`, no `tasks.json`), the container images are Release-published with no attach surface (no watch, no diagnostic port, no debugger), and no document — README, development-style, or configuration — explains how to get a breakpoint against either API. A developer following the Quickstart into `docker compose up` has no path to a debugger; mixing launch modes silently collides on the host ports (5264/5265) and splits the SQLite stores (`db/` under the CmsWebhook project for host runs vs the `queue-db` volume for the stack).

## What Changes

- **A documented debugging workflow** covering the three surfaces — host `dotnet run`/F5, the devcontainer, and the composed containers — with when to use each, plus explicit warnings for the two real traps: the port collision when the stack and a host launch run at the same time, and the divergent store locations per mode.
- **A container debugging mode** — a `docker-compose.dev.yml` override that runs both APIs from source (`dotnet watch`, Debug build) against the **host `db/` stores shared with F5 runs** (the debug containers bind-mount `src/CmsWebhook/CmsWebhook.Api/db`), so breakpoints, hot reload, and data are shared across the dev surfaces — while plain `docker compose up` keeps building the production Release images against their own `queue-db` volume (the default stack is unchanged).
- **VS Code wiring** — `.vscode/tasks.json` (compose orchestration: up prod-like, up debug, down, reset) and `.vscode/launch.json` (a compound "both APIs on the host" profile plus container-attach profiles).
- **Docs updates** — a README "Debugging" section and `docs/development-style.md` conventions; a short note in `docs/configuration.md` on the debug override.

Non-goals: no changes to application code, ports, passwords, or any API behavior; no changes to the production images, the compose default, CI, or the pending `aws-deployment` change.

## Capabilities

### New Capabilities

- `developer-debugging`: the repository SHALL ship a documented, working debugging workflow for both APIs — covering host debugging, the devcontainer, and in-container debugging of the composed stack — without altering the default production-image stack.

### Modified Capabilities

<!-- None: this change introduces no requirement changes to existing product capabilities (auth, cms-webhook-api, users-api, configuration, ci-quality-gates). `containerization` remains an unsynced delta of the containerize-apis change and is not touched. -->

## Impact

- **Added files**: `.vscode/tasks.json`, `.vscode/launch.json`, `docker-compose.dev.yml`, README "Debugging" section, `docs/development-style.md` conventions, `docs/configuration.md` note.
- **Untouched**: `src/`, `tests/`, `scripts/`, `.github/workflows/`, the two production `Dockerfile`s, `docker-compose.yml` (default stack), all product capabilities.
- **Editor/tooling only** — no runtime or CI dependency; developers without VS Code still get the documented CLI commands.
