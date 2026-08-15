## Context

See `proposal.md` — Why. Current state that shapes the approach:

- `launchSettings.json` exists for both APIs (Development, `5264`/`5265`) — host F5 works via C# Dev Kit out of the box, but nothing documents it as *the* debugging path.
- The composed stack (`docker-compose.yml`) maps host `5264`→`8080` and `5265`→`8080`; both containers run **Release** publishes (`Dockerfile` → `dotnet publish -c Release`, `USER app`, bare `ENTRYPOINT`) — no source mount, no watch, no diagnostic port, no debugger: structurally unattachable today.
- The `init` service already proves the SDK-image + repo-bind-mount pattern (`image: mcr.microsoft.com/dotnet/sdk:9.0`, `working_dir: /repo`, `volumes: [.:/repo, queue-db:/data]`) — the debug override reuses it, no new Dockerfile needed.
- The devcontainer (Path A, per earlier decision) has no Docker access: it can F5 host runs inside itself, but cannot run or debug the compose stack.
- Grep for "debug" in docs: zero hits. The port-sync between compose and `launchSettings` (a deliberate design choice) is exactly what makes mixing modes collide silently.

## Goals / Non-Goals

**Goals:**
- A debugging workflow a new developer can actually follow, covering all three surfaces and naming the two traps (port collision, split stores).
- In-container debugging of the composed stack (hot reload + attach) against the *same* stores and ports as the production-image stack.
- Editor ergonomics: tasks + launch profiles so orchestration and attach happen without leaving VS Code.
- Plain `docker compose up` stays byte-for-byte the production-image stack (the containerization capability's contract).

**Non-Goals:**
- Changing application code, ports, passwords, or any API behavior.
- Changing the production images, `docker-compose.yml`, CI, or the E2E/smoke layers.
- Docker access inside the devcontainer (revisiting the Path A decision) — that's a separate change if ever wanted.
- Anything in the `aws-deployment` scope.

## Decisions

### D1: A documented decision tree over three debugging surfaces

Docs (README "Debugging" + `docs/development-style.md`) present the surfaces in order of simplicity:

```
                    DEBUGGING SURFACES
   ┌───────────────────┬──────────────────┬──────────────────────┐
   │  1. Host F5       │  2. Devcontainer │  3. Containers       │
   │  dotnet run       │  dotnet run     │  docker-compose.dev   │
   │  (launchSettings) │  inside ctr     │  (source + watch)     │
   ├───────────────────┼──────────────────┼──────────────────────┤
   │ simplest;         │ same as host,    │ full stack parity;   │
   │ needs .NET SDK    │ isolated env;    │ hot reload + attach; │
   │                   │ ports forwarded  │ needs host Docker    │
   └───────────────────┴──────────────────┴──────────────────────┘
        stores: src/CmsWebhook/CmsWebhook.Api/db/  (ALL THREE share it)
        stores: queue-db volume  (production-image stack only)
```

The debug containers bind-mount the same `db/` folder host runs use (Option B), so entities written by an F5 session are visible in the debug stack and vice versa — one store across the dev surfaces. The remaining hazards are stated explicitly: **port collision** (stack and host launches both bind `5264`/`5265` — stop one before starting the other) and the **production-image stack's isolated `queue-db` volume** (entities written there are invisible to the dev surfaces). Attach profiles target the host Docker engine, so they require VS Code running on the host OS — the devcontainer has no Docker access (Path A).

Rationale: the docs gap is the actual bug — the configs already half-exist (`launchSettings`), they're just undiscoverable and the container path is missing entirely. Alternatives considered: a single "debug everything" mega-command — rejected, each surface has a legitimately different audience and environment.

### D2: Debug mode = explicit `docker-compose.dev.yml`, not `.override.yml`

A separate override file invoked explicitly, e.g. `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d` (wrapped in a VS Code task and a documented one-liner). Rationale: `docker-compose.override.yml` is applied *automatically* by plain `docker compose up`, which would silently change the default stack from production images to watch/debug builds — violating the containerization capability's contract and slowing the recommended path. Explicit `-f` composition keeps `docker compose up` prod-like and makes the debug mode a deliberate choice.

The override:

- reuses the proven SDK-image + repo-mount pattern from `init` (`image: mcr.microsoft.com/dotnet/sdk:9.0`, `volumes: [.:/repo, ...]`);
- bind-mounts the **host `db/` folder** (`src/CmsWebhook/CmsWebhook.Api/db`) at `/data` in place of the `queue-db` volume, so the debug containers share stores with host F5 runs (Option B) — the same `ConnectionStrings__*` env vars point at `/data/*.db`, and the base port mappings (`5264:8080`, `5265:8080`) are kept, with the debug apps binding `http://0.0.0.0:8080` inside the container exactly like the production images (binding `localhost` inside a container is loopback-only and unreachable through the published host port); the override sets only `build: !reset null` to clear the base build so the plain SDK image is used — volumes are merged by target, not via `!reset`;
- runs as **root** (the SDK image's default, like `init`) so the C# extension can drop its debugger (`vsdbg`) into the container for attach — the production images keep `USER app`;
- re-points `init` at the same bind mount and **drops the base `chown -R 1654:1654 /data`** (chown'ing a host folder from inside a container would rewrite host file ownership), keeping the inherited repo mount so `scripts/init-db.sh` still runs from `working_dir: /repo`; seeding stays idempotent, so an already-seeded host `db/` is left unchanged.

The init ordering (`depends_on: init: service_completed_successfully` and `cms-api` before `users-api`) is preserved so the debug stack provisions the shared schema identically.

**Why the debug surface can share a host folder and the production-image stack cannot:** the SDK containers run as root and already bind-mount the repo, so uid ownership is a non-issue there; the production images run as the unprivileged `app` user (uid 1654), and writing into a host-owned folder from that uid breaks on Linux hosts and on macOS's slow bind-mount SQLite I/O. The `queue-db` volume remains the production-image stack's store by design.

### D3: VS Code wiring — tasks orchestrate, launch profiles attach

`.vscode/tasks.json`:
- `compose: up` (prod-like), `compose: up (debug)` (the `-f` composition above), `compose: down`, `compose: reset` (`down -v`).

`.vscode/launch.json`:
- a compound `Both APIs (host)` profile launching the two `launchSettings` `http` profiles together — F5 with two debuggers;
- a `Attach: CmsWebhook (container)` / `Attach: Users (container)` pair targeting the debug-mode containers, so "run task, then attach" is the in-container flow.

Rationale: the C# extension already derives host launch profiles from `launchSettings.json`, so `launch.json` adds only what's genuinely missing — a compound start and the container-attach targets. Tasks are the single place the awkward `-f` invocation is encoded, so docs and the editor share one source of truth (tasks can also be referenced from README commands).

### D4: Docs are the deliverable, configs support them

The README "Debugging" section is the primary artifact (D1's decision tree + the exact commands), `docs/development-style.md` records the conventions (when each surface, the collision/store traps, why the override is explicit), and `docs/configuration.md` gains a short "Debugging in containers" note linking to the README. The existing E2E/smoke layers stay the guard against run-contract drift.

## Risks / Trade-offs

- [Two stacks can't run simultaneously (same ports)] → documented explicitly in D1; the debug flow expects the prod-like stack stopped.
- [vsdbg attach inside containers depends on C# extension behavior] → hot reload (`dotnet watch`) is the guaranteed baseline of debug mode; attach is documented as "works with the C# extension" without being load-bearing.
- [Debug override runs as root] → dev-only tooling, never a production image; mirrors the existing `init` service precedent.
- [Temptation to switch to `.override.yml`] → D2 rationale is recorded in the file header comment; the containerization spec's "default stack unchanged" scenario is the guardrail.
- [Linux hosts: debug containers create root-owned files in the shared `db/`] → documented caveat (`sudo chown -R $(id -u) src/CmsWebhook/CmsWebhook.Api/db` after a debug session); Docker Desktop maps uids and needs nothing.
- [Production-image stack still uses a separate volume] → deliberate: uid/perf/reset reasons in D2; stated as the one remaining store split in the docs.

## Migration Plan

Additive only: new files (`.vscode/tasks.json`, `.vscode/launch.json`, `docker-compose.dev.yml`) and doc sections. Nothing existing changes; the default stack, images, scripts, CI, and all `dotnet run` workflows keep working untouched. The debug override's store change (volume → host `db/` bind mount) affects only the opt-in debug mode.

## Open Questions

None that affect the specs, approach, or task breakdown. (Whether the devcontainer should ever gain Docker access is deliberately deferred — it would change the attach story and is out of scope here.)
