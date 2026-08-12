# Queue API Exercise

This repository aims to store the implementation of a .Net 9 platform-agnostic API solution which will accept messages which will eventually be processed resulting in their corresponding changes in a database.

## Development environment
To simplify the development experience, a Dockerfile is provided as dev container with all dependencies and some VSCode extensions available.

### Running instructions
Following instructions assume that you're running a terminal at the project's root within the provided dev container for the first time:

#### General / whole project
1. Execute `dotnet restore`
1. Execute `dotnet build`

#### CMS API

##### Local environment setup
1. Initialize the credential store (one time per checkout):
   `./scripts/init-db.sh` — creates `db/queue-auth.db` with the `cms-webhook` user.
   - Optional arguments: `./scripts/init-db.sh [username] [password]`
   - Without a password, it uses the local-development default below. The script is idempotent; re-running it is safe.

> **Credentials & configuration:** credentials live in the SQLite credential store at `db/queue-auth.db` (gitignored, provisioned by `scripts/init-db.sh`), not in environment variables. The store location is configurable via `ConnectionStrings:AuthDb` (e.g. the `ConnectionStrings__AuthDb` environment variable), and the reserved cms username via `Auth:CmsUsername` (e.g. the `Auth__CmsUsername` environment variable). To change a seeded user's password, delete `db/queue-auth.db` and re-run the script (re-running with a different password leaves the existing user unchanged). The local-development default password used by `scripts/init-db.sh` is `0f6c3c5a-9b2e-4f7d-8a1c-2e5b9d7f3a61` — DO NOT use it outside local development.

> **TLS requirement:** Basic authentication transmits credentials as base64, which is *not* encryption. Production deployments of `CmsWebhook.Api` MUST serve over TLS (HTTPS); the plain-http profile in `launchSettings.json` is for local development only.

##### Execution
1. Execute `dotnet run --project src/CmsWebhook/CmsWebhook.Api` (the API fails fast at startup if the store is missing or not initialized).
1. In a new terminal window, execute `curl -X GET -u  <username>:<password> http://127.0.0.1:5264/` and you should receive a `"Hello World!"` response.

## Development approach
When given an exercise for an interview a common temptation is to over-engineer as a way to "flex" or display prowess, however I've chosen to tackle this as if it was a requirement coming from a client: taking the list of requirements at face value, not over-thinking abstractions and bolting-on external dependencies when they can be avoided.

Regular instinct and "current trends" / "best" practices would have guided me to a "standard" solution of "just" picking up RabbitMQ and/or a host of libraries, however I'm deliberately choosing to keep it as simple as possible at each increment.

Speaking of "increments", I will be developing this solution following TDD as much as possible.

### AI Assistance
I've been encouraged to rely on AI assistance for the production of this solution, however I won't just be delegating the full coding / doing SDD. I prefer to guide Agents one change at a time, and to write relevant text (such as this README) by hand whenever I want my voice to be preserved. Then regarding DSL and "specs", I will take a "code as source of truth" approach, where implementation code and naming conventions will explicitly show the "what" and "how", and always ensuring that Summary comments explaining the "why" are properly present.

#### Installing FREEBUFF
Due to budget constraints, I'm using [FREEBUFF](https://github.com/CodebuffAI/freebuff) as coding assistant since it's good enough for my purposes. I'm keeping it out of Dockerfile intentionally, but as any other automated harness, it should better be run sandboxed. I'm also using [OpenSpec](https://github.com/Fission-AI/OpenSpec/) as change tracker, since it's a tool I have been meaning to try and decided this project may be a good chance to test it. I recommend installing these tools within the devcontainer's terminal via [pnpm](https://pnpm.io/) by executing:

```bash
wget -qO- https://get.pnpm.io/install.sh | ENV="$HOME/.bashrc" SHELL="$(which bash)" bash - # Installing pnpm since it's a safer alternative to npm
source ~/.bashrc # Reloading the terminal
pnpm runtime set node lts -g
pnpm install -g freebuff
pnpm install -g @fission-ai/openspec@latest
[ -d openspec/ ] || openspec init # If the openspec folder doesn't exist (ie, you're starting a new project, you must initialize first)
pnpm install -g openlore # Installs OpenLore to keep track of development drift and to incorporate manual code changes into the spec if need be
[ -f .openlore/index-bundle.olbundle ] && openlore import .openlore/index-bundle.olbundle || openlore install # Checks if openlore is already initialized, otherwise does so
openlore doctor # Checks openlore has been correctly initialized
openlore verify # Verifies the current specs' validity
# openlore drift --install-hook # currnely wrongly detects skill files as drift, run `openlore drift` manually before commit! See https://github.com/clay-good/OpenLore/issues/350
```

>  Note: I've given the above sequence the flexibility to be ran in a new project, should you want to copy them into your own set-up.

## Architecture / Plan
In tandem of the KISS principle, it would be an oversight in my years of experience to not treat this project as if it had plans to grow in the future, meaning I will aim to keep a clear separation of boundaries and domains within a Modular Monolith, following a Ports+Adapters and Clean architecture. Then, given the fact that from the start there are requirements for event handling and distinct flows (CMS VS Users), following an Event-Driven architecture (not Event-Sourcing for now) with CQRS also in place feels natural. Observability via logging and possibly OTEL will be approached as soon as justified.

To get usable value ASAP, I will focus on implementing visible API implementation first, adding inner domain and infrastructure (and simple UI?) later as needed.