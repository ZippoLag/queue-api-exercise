# Debugging

**General concept — what the debugging surfaces are.** An API can be run in several ways for development: directly on the host OS, inside the devcontainer, or in Docker containers. Each is a "surface". This project documents three, in order of simplicity, and the rules that keep them from colliding.

**In this project** the three surfaces are:

1. **Host** — the simplest; F5 in VS Code or `dotnet run` from a console.
2. **Devcontainer** — the same host surface, from the devcontainer console.
3. **Containers (debug override)** — both APIs from source with `dotnet watch`, full stack parity with hot reload.

**Why only one at a time.** All three dev surfaces bind the same host ports (`5264`/`5265`), and the debug containers bind-mount the same `db/` folder the host runs use. Running more than one surface at once is a port collision or a store clash — see [Mode-mixing traps](#mode-mixing-traps).

## 1. Host — simplest

F5 in VS Code — the `Both APIs (host)` compound profile or the per-project profiles in `.vscode/launch.json` — or from a console:

```bash
./scripts/init-db.sh          # once: seeds the credential store
# in two terminals:
dotnet run --project src/CmsWebhook/CmsWebhook.Api   # CMS Webhook API on :5264
dotnet run --project src/Users/Users.Api             # Users API on :5265
```

Breakpoints bind, hot reload applies, and the stores live under `src/CmsWebhook/CmsWebhook.Api/db/` — the same files the debug containers use.

## 2. Devcontainer

Same as host debugging, from the devcontainer console (ports `5264`/`5265` are forwarded to your host browser). The devcontainer has **no Docker daemon**, so it is for this surface only — not the containers below.

> **Devcontainer port collision**: the devcontainer is configured to always forward the ports, so if the devcontainer is open you cannot successfully run these projects outside of it in your host OS, whether via `docker compose...` or `dotnet run...`.

## 3. Containers — full stack parity, hot reload

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

Runs both APIs **from source** with `dotnet watch` (Debug builds, hot reload) against the **same host `db/` stores your F5 runs use** — entities written in either surface appear in the other. Requires Docker on the host and Docker Compose ≥ 2.24.4 (the override uses the `!reset` merge tag). The `.vscode/tasks.json` `compose: up (debug)` task runs the same command; tear down with the same `-f` file set (`... down`).

**Attaching to the containers** — the `Attach: ... (container)` profiles in `.vscode/launch.json` target processes on your host's Docker engine, so they require VS Code running **on the host OS** (the devcontainer has no Docker access). Run the `compose: up (debug)` task, then attach. The `host` profiles work from any VS Code instance with the .NET SDK — including inside the devcontainer.

> **Linux hosts only**: files the debug containers create in `db/` are owned by root; after a debug session run `sudo chown -R $(id -u) src/CmsWebhook/CmsWebhook.Api/db` to restore host ownership. Docker Desktop (macOS/Windows) maps uids and needs none of this.

## Store-sharing model

All three dev surfaces share **one store**: `src/CmsWebhook/CmsWebhook.Api/db/` — the debug containers bind-mount that same folder.

**Why.** The whole point of the dev surfaces is that a host F5 session and the debug stack see the same data, so an entity written in one appears in the other.

Only the production-image stack (`docker compose up` without `-f`) uses the `queue-db` volume, so data written there is invisible to the dev surfaces (and vice versa).

```
        dev surfaces (host, devcontainer, debug containers)
                          │
              bind-mount / share
                          ▼
        src/CmsWebhook/CmsWebhook.Api/db/   ← one store, all dev surfaces

        production-image stack (plain `docker compose up`)
                          │
                named volume `queue-db`
                          ▼
                       /data in-container        ← isolated, not the dev store
```

## Mode-mixing traps

- **Port collision** — the stack and host launches both bind `5264`/`5265`; stop one before starting the other.
- **Devcontainer port forwarding** — the devcontainer always forwards the ports, so having it open blocks host-OS runs of the same projects (see [Devcontainer](#2-devcontainer)).
- **The debug override is explicit, not automatic.** `docker-compose.dev.yml` is applied only via `-f docker-compose.yml -f docker-compose.dev.yml`, never merged automatically. Naming it `docker-compose.override.yml` would silently turn plain `docker compose up` into watch/debug builds, breaking the "default stack unchanged" contract. Compose merge semantics matter here: volumes are a "unique resource" merged by container target, so the override's plain volume list replaces the base `queue-db` mount — and `!reset` is used exactly once, to clear the base `build:` so the plain SDK image is used (`!reset` clears an attribute; it does not replace a list). Requires Compose ≥ 2.24.4.
- **Watch is the guaranteed baseline; attach is the bonus.** Debug mode runs `dotnet watch` (Debug builds, hot reload) from the SDK image with the repo bind-mounted at `/repo`; the C# extension can additionally attach to the `dotnet` process inside the container (`Attach: ... (container)` profiles, `sourceFileMap: /repo → workspace`). The production images are never touched for this.
- **The override's `init` drops the base `chown`** (chown'ing a host folder from inside a container rewrites host ownership); on Linux hosts, chown `db/` back after a debug session (see [Containers](#3-containers--full-stack-parity-hot-reload)).

## See also

- [Configuration](configuration.md) — where the shared stores live and how their paths are resolved.
- [Testing](testing.md) — how the debug-mode stacks are exercised by the E2E suite.
- [Tooling](tooling.md) — the editor/tooling setup that wires the launch and attach profiles.
- README [Quickstart](../README.md#quickstart) — the production-image stack, for a one-command run.
