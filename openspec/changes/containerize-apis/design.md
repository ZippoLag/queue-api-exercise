## Context

See `proposal.md` — Why. Current state that shapes the approach:

- The only existing Dockerfile (`.devcontainer/Dockerfile`) is a full **dev** image (SDK, workloads, `tail -f /dev/null` entrypoint) — unsuitable for production.
- Both APIs are plain ASP.NET Core minimal APIs on `net9.0`; stores are two SQLite files addressed via env-var connection strings; `scripts/init-db.sh` seeds the credential store and is idempotent.
- The APIs fail fast at startup when the credential store is missing/unseeded — so the compose `init` service must complete before either API starts.
- `smoke-e2e.sh` already proves the exact env-var contract a container needs (`ConnectionStrings__AuthDb`, `ConnectionStrings__CmsDb`, `ASPNETCORE_ENVIRONMENT`); it also shows the CMS API must start first to provision the shared schema.

## Goals / Non-Goals

**Goals:**
- Production images for both APIs from official .NET 9 base images, non-root, binding all interfaces.
- One command (`docker compose up`) runs init + both APIs against one shared volume, keeping the README curl walkthrough byte-for-byte valid.
- Fix the devcontainer port forwarding so the documented onboarding path actually works.

**Non-Goals:**
- AWS infrastructure and pipelines — that is the separate `aws-deployment` change (this change's images unlock its Fargate variant; nothing here deploys anywhere).
- Replacing `dotnet run` local development; compose is the recommended path, the manual sequence stays documented.
- Changing application code, ports, passwords, or any API behavior.

## Decisions

### D1: Multi-stage Dockerfile per API, publish-on-SDK → copy-on-runtime

Each API gets its own `Dockerfile` next to the `.csproj`:

```
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# copy the solution, Directory.Build.props, global.json, and only this API's source
RUN dotnet publish <Api>.csproj -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
USER app
ENTRYPOINT ["./<Api>"]
```

Rationale: the standard, auditable two-stage pattern; `USER app` exists on the aspnet image; `aspnet:9.0` (not `sdk`) keeps images small. Alternatives considered: **chiseled images** (`-chiseled` variants) — smaller and more secure, but newer; noted as a future swap, not this change. **Single shared Dockerfile with build args** — rejected: two simple per-project files are easier to read and build independently.

### D2: `docker compose` — init one-shot, shared named volume, host ports 5264/5265

Topology:

```
docker compose up
├── init      (one-shot: scripts/init-db.sh against /data/queue-auth.db, default passwords)
├── cms-api   (build src/CmsWebhook/CmsWebhook.Api → host 5264 → container 8080)
└── users-api (build src/Users/Users.Api        → host 5265 → container 8080)
        └── volume "queue-db" mounted at /data on init, cms-api and users-api
```

- `init` uses the `sdk:9.0` image mounting the repo, runs the **existing** `scripts/init-db.sh` (no new seeding logic), and `cms-api`/`users-api` declare `depends_on: init: condition: service_completed_successfully`.
- A **named volume** (not a bind mount) for the stores: portable across Docker Desktop/macOS/Windows, and it sidesteps macOS bind-mount I/O slowdowns for SQLite WAL. Re-seeding is an explicit `docker compose down -v`.
- Both APIs get `ASPNETCORE_ENVIRONMENT=Development` and the two `ConnectionStrings__*` env vars pointing at `/data/*.db`. Host ports map 5264/5265 exactly as `launchSettings.json` does, so the README curls are unchanged.
- `init`'s first run is intentionally slow (`dotnet run tools/AuthDbInit` compiles); acceptable for a one-shot.

### D3: Devcontainer `forwardPorts` fixed to `[5264, 5265]`

The current `5000/5001/8080/8081` matches nothing the apps bind. Setting the actual ports makes the documented devcontainer path work from the host browser. (Compose-in-devcontainer is a follow-up nicety, not required here.)

### D4: `.dockerignore` per project

Each API's `.dockerignore` excludes `bin/`, `obj/`, `*.user`, etc., so the build context sent to the daemon stays small. The build context is the repo root (the csproj references `..\..\Shared\QueueApi.Auth`), so the ignore files must not exclude `src/Shared` or the referenced projects.

## Risks / Trade-offs

- [Named volume hides stores from host editors] → `docker compose down -v` documented as the reset path; `docker compose exec` gives shell access when inspection is needed.
- [Host port 5264/5265 collisions] → compose respects standard `ports:` overrides; README notes changing them only requires editing the compose file.
- [Image size / supply chain] → runtime-only stage keeps images small; base images are the official Microsoft registry with digest-pinning noted for later hardening.
- [Two sources of truth for the run contract (compose vs `dotnet run` + launchSettings)] → accepted: both must keep ports and env-var names aligned; the E2E smoke suite (`smoke-e2e.sh`) already pins the env-var contract and stays the guard against drift.

## Migration Plan

No migration — this is additive tooling. Existing `dotnet run` workflows keep working untouched. Developers adopting compose simply start using `docker compose up`; nothing existing is removed.

## Open Questions

- None that affect the specs, approach, or task breakdown. (Chiseled images, digest pinning, and compose profiles for the devcontainer are all deferrable enhancements.)
