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
        stores: db/  (host & devcontainer, same mounted repo)
        stores: queue-db volume  (containers)
```

Both traps are stated explicitly wherever modes can be mixed: **port collision** (stack and host launches both bind `5264`/`5265` — stop one before starting the other) and **split stores** (entities written in one mode are invisible in the other).

Rationale: the docs gap is the actual bug — the configs already half-exist (`launchSettings`), they're just undiscoverable and the container path is missing entirely. Alternatives considered: a single "debug everything" mega-command — rejected, each surface has a legitimately different audience and environment.

### D2: Debug mode = explicit `docker-compose.dev.yml`, not `.override.yml`

A separate override file invoked explicitly, e.g. `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d` (wrapped in a VS Code task and a documented one-liner). Rationale: `docker-compose.override.yml` is applied *automatically* by plain `docker compose up`, which would silently change the default stack from production images to watch/debug builds — violating the containerization capability's contract and slowing the recommended path. Explicit `-f` composition keeps `docker compose up` prod-like and makes the debug mode a deliberate choice.

The override:

- reuses the proven SDK-image + repo-mount pattern from `init` (`image: mcr.microsoft.com/dotnet/sdk:9.0`, `volumes: [.:/repo, queue-db:/data]`);
- runs `dotnet watch --project src/<Api>` in Debug so source edits hot-reload, with the same `ConnectionStrings__*` env vars pointing at `/data/*.db` and the same host ports `5264`/`5265` → in-container port from `launchSettings`/`ASPNETCORE_URLS` (Kestrel binds `localhost` inside the dev images, which is correct: the debugger attaches locally);
- runs as **root** (the SDK image's default, like `init`) so the C# extension can drop its debugger (`vsdbg`) into the container for attach — the production images keep `USER app`.

The init ordering (`depends_on: init: service_completed_successfully` and `cms-api` before `users-api`) is preserved so the debug stack provisions the shared schema identically.

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
- [Store divergence between `db/` and the volume remains] → documented as a trap (D1); unifying stores would change dev behavior beyond this change's scope.

## Migration Plan

Additive only: new files (`.vscode/tasks.json`, `.vscode/launch.json`, `docker-compose.dev.yml`) and doc sections. Nothing existing changes; the default stack, images, scripts, CI, and all `dotnet run` workflows keep working untouched.

## Open Questions

None that affect the specs, approach, or task breakdown. (Whether the devcontainer should ever gain Docker access, and whether stores should be unified across surfaces, are deliberately deferred — both would change the approach and are out of scope here.)
