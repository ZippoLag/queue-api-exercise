## 1. Production Dockerfiles

- [ ] 1.1 Add `src/CmsWebhook/CmsWebhook.Api/Dockerfile` — multi-stage (`sdk:9.0` build → `aspnet:9.0` runtime), non-root `USER app`, `ASPNETCORE_URLS=http://0.0.0.0:8080`, `EXPOSE 8080`, entrypoint `./CmsWebhook.Api`
- [ ] 1.2 Add `src/Users/Users.Api/Dockerfile` — same pattern, entrypoint `./Users.Api`
- [ ] 1.3 Add `.dockerignore` for each API project (exclude `bin/`, `obj/`, `*.user`, etc.; keep `src/Shared` and referenced projects in the context)
- [ ] 1.4 Build both images from the repo root and verify each container boots, `/health` returns 200 on the published port, and the process runs as a non-root user

## 2. Local compose stack

- [ ] 2.1 Add root `docker-compose.yml` — `init` one-shot (runs `scripts/init-db.sh` on the shared volume with the local-development default passwords), `cms-api` (host 5264 → container 8080) and `users-api` (host 5265 → container 8080), `depends_on: init: condition: service_completed_successfully`, shared named volume mounted at `/data`, both APIs get `ASPNETCORE_ENVIRONMENT=Development` + the two `ConnectionStrings__*` env vars pointing at `/data/*.db`
- [ ] 2.2 Run `docker compose up` from a clean state and verify the README curl walkthrough verbatim (CMS publish → regular-user listing → admin disable/enable → `cms-webhook` 403 on Users API)
- [ ] 2.3 Verify the reset path: `docker compose down -v` + `up` re-seeds the credential store and the walkthrough passes again

## 3. Devcontainer and docs

- [ ] 3.1 Fix `.devcontainer/devcontainer.json` `forwardPorts` to `[5264, 5265]`
- [ ] 3.2 Rewrite the README Quickstart to lead with `docker compose up`, keeping the manual `dotnet run` sequence as the documented alternative
- [ ] 3.3 Update `docs/configuration.md` with the container binding (`ASPNETCORE_URLS`), the shared-volume layout, and the re-seed note
