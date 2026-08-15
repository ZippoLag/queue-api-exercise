## Why

The quickstart works, but the first-run experience has real friction: both APIs run in two separate terminals, every helper script is bash-only (a wall on Windows without WSL/Git Bash), the devcontainer's forwarded ports (`5000/5001/8080/8081`) don't match the ports the apps actually bind (`5264/5265`), and — the bigger blocker — the repo ships **no production container image at all** (the only Dockerfile is the dev container's). That last gap rules out every managed container runtime (ECS Fargate, App Runner, Beanstalk-docker) for the planned AWS deployment. This change ships production images and a one-command local stack.

## What Changes

- **Two multi-stage production Dockerfiles** — one per API (`src/CmsWebhook/CmsWebhook.Api/Dockerfile`, `src/Users/Users.Api/Dockerfile`): `sdk:9.0` build stage publishing Release, `aspnet:9.0` runtime stage running as a non-root user, binding `http://0.0.0.0:8080` (the container analogue of the deployment binding requirement), plus `.dockerignore` files to keep the build context small.
- **`docker-compose.yml` at the repo root** — services `cms-api` (host port `5264`) and `users-api` (host port `5265`) on one shared named volume holding the two SQLite stores, plus an `init` one-shot service that seeds the credential store via the existing `scripts/init-db.sh`/`tools/AuthDbInit` before the APIs start (`depends_on` with `service_completed_successfully`). The default passwords are unchanged, so the existing curl walkthrough in the README keeps working verbatim.
- **Devcontainer port-forward fix** — `.devcontainer/devcontainer.json` `forwardPorts` becomes `[5264, 5265]`, matching what `launchSettings.json` actually binds, so the documented devcontainer path can reach the APIs from the host browser.
- **Docs** — README quickstart gains `docker compose up` as the primary one-command path (the manual `dotnet run` sequence stays as the alternative); `docs/configuration.md` notes the container binding (`ASPNETCORE_URLS`) and volume layout.
- No application code changes; no **BREAKING** changes.

## Capabilities

### New Capabilities

- `containerization`: the repository ships production container images for both APIs and a local container orchestration (`docker compose`) that runs the full stack — both APIs plus credential-store initialization — against one shared store, so the documented curl walkthrough works unchanged.

### Modified Capabilities

- None — the devcontainer/compose changes are tooling, and no existing spec-level behavior (auth, configuration precedence, API contracts, CI gates) changes.

## Impact

- **New files**: `src/CmsWebhook/CmsWebhook.Api/Dockerfile`, `src/Users/Users.Api/Dockerfile`, per-project `.dockerignore`, root `docker-compose.yml`.
- **Edited files**: `.devcontainer/devcontainer.json`, `README.md`, `docs/configuration.md`.
- **No changes** to `src/` application code, tests, or `QueueApi.slnx`.
- **Dependencies**: only the official `mcr.microsoft.com/dotnet/{sdk,aspnet}:9.0` base images; nothing new in the .NET dependency graph.
- **Unblocks**: the `aws-deployment` change's container-based alternatives (Fargate/App Runner) — the EC2 path there does not require this change, so the two changes stay independently shippable.
