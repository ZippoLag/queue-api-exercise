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
1. Set the required credentials environment variables (the API fails fast at startup without them):
   - `AUTH_CMS_USERNAME` — the configured cms username, between 10 and 20 characters
   - `AUTH_CMS_PASSWORD` — the configured cms password
1. Execute `dotnet run --project src/CmsWebhook/CmsWebhook.Api`
1. In a new terminal window, execute `curl -X GET -u  <username>:<password> http://127.0.0.1:5264/` and you should receive a `"Hello World!"` response.

> **TLS requirement:** Basic authentication transmits credentials as base64, which is *not* encryption. Production deployments of `CmsWebhook.Api` MUST serve over TLS (HTTPS); the plain-http profile in `launchSettings.json` is for local development only.

## Development approach
When given an exercise for an interview a common temptation is to over-engineer as a way to "flex" or display prowess, however I've chosen to tackle this as if it was a requirement coming from a client: taking the list of requirements at face value, not over-thinking abstractions and bolting-on external dependencies when they can be avoided.

Regular instinct and "current trends" / "best" practices would have guided me to a "standard" solution of "just" picking up RabbitMQ and/or a host of libraries, however I'm deliberately choosing to keep it as simple as possible at each increment.

Speaking of "increments", I will be developing this solution following TDD as much as possible.

### AI Assistance
I've been encouraged to rely on AI assistance for the production of this solution, however I won't just be delegating the full coding / doing SDD. I prefer to guide Agents one change at a time, and to write relevant text (such as this README) by hand whenever I want my voice to be preserved. Then regarding DSL and "specs", I will take a "code as source of truth" approach, where implementation code and naming conventions will explicitly show the "what" and "how", and always ensuring that Summary comments explaining the "why" are properly present.

#### Installing FREEBUFF
Due to budget constraints, I'm using [FREEBUFF](https://github.com/CodebuffAI/freebuff) as coding assistant since it's good enough for my purposes. I'm keeping it out of Dockerfile intentionally, but as any other automated harness, it should better be run sandboxed. I'm also using [OpenSpec](https://github.com/Fission-AI/OpenSpec/) as change tracker, since it's a tool I have been meaning to try and decided this project may be a good chance to test it. I recommend installing these tools within the devcontainer's terminal via [pnpm](https://pnpm.io/) by executing:

```bash
wget -qO- https://get.pnpm.io/install.sh | ENV="$HOME/.bashrc" SHELL="$(which bash)" bash -
source ~/.bashrc
pnpm runtime set node lts -g
pnpm install -g freebuff
pnpm install -g @fission-ai/openspec@latest
```

This tool correctly picks up `AGENTS.md`, in which I add details regarding project structure, coding style, etc.

## Architecture / Plan
In tandem of the KISS principle, it would be an oversight in my years of experience to not treat this project as if it had plans to grow in the future, meaning I will aim to keep a clear separation of boundaries and domains within a Modular Monolith, following a Ports+Adapters and Clean architecture. Then, given the fact that from the start there are requirements for event handling and distinct flows (CMS VS Users), following an Event-Driven architecture (not Event-Sourcing for now) with CQRS also in place feels natural. Observability via logging and possibly OTEL will be approached as soon as justified.

To get usable value ASAP, I will focus on implementing visible API implementation first, adding inner domain and infrastructure (and simple UI?) later as needed.