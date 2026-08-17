# Development Guidelines

## Context
You are a professional software developer working in a **.NET 9** codebase for a real business.

## Operational rules
- Prefer using the `dotnet` CLI for operations over raw bash scripts when applicable.
- Do not re-implement standard .Net classes, properties, methods, etc, if standard implementation is available: begin each new architectural change by checking official Microsoft Learn docs through their MCP `https://learn.microsoft.com/api/mcp` or navigating to: `https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/?view=aspnetcore-9.0`.
- Follow clean architecture and domain driven design, do not cross boundaries
- Follow CQRS: writes and reads should be independent

## Project Structure
- Domain knowledge: `@docs/dsl_glossary.md` 
- Current architecture plan: `@docs/architecture.md`
- Tests: `@test/<project>/` — must explicitly cite source business rules
- Implementation: `@src/<project>/`
- XML documentation files are enabled (with `GenerateDocumentationFile` set to `true` in all `.csproj` files), look for `*/bin/*.xml` in each built project when you need to analize the implementation state quickly. Run `dotnet build` if they are not present.
- Update the `README.md` and all linked documents whenever specs change, are synced or archived.

## Documentation
`README.md` and `docs/**` are the canonical human documentation (the DocFX site is a generated view — never a separate copy). Keep them up to date whenever behavior, configuration, or tooling changes, and update `toc.yml` and the README docs index when pages are added, moved, or removed.

- **One fact, one home**: never replicate prose across files. Each fact lives in exactly one file; other files link to it (relative markdown links). Command blocks may embed the values they execute (ports, passwords, paths) — that is not replication.
- **Per-topic template**: every topic section separates general concept → in-this-project specifics → why (reasoning) → diagram → sample/commands → see-also links.
- **Domains don't cross files**: each file owns its conceptual domain (table below); higher-level files condense and link down to lower-level runbooks instead of restating them.
- **Naming**: context-appropriate spellings are intentional — `cms-webhook` is the reserved username (never renamed), `CmsWebhook` is the C# naming, `CMS Webhook` is the prose title. `docs/dsl_glossary.md` is the single place that reconciles them as synonyms.
- **Voice**: neutral third person; reasoning is written as an explicit "why" next to the fact it justifies.
- **Archived invariant**: never modify `docs/archived/initial_requirements.md`.

| File | Owns |
|---|---|
| `README.md` | identity, quickstart, pointer hub, docs index |
| `docs/architecture.md` | system overview, design decisions, behavior |
| `docs/configuration.md` | configuration chain, secrets, DB paths, TLS |
| `docs/development-style.md` | development approach and conventions |
| `docs/dsl_glossary.md` | terminology + naming synonyms |
| `docs/debugging.md` | debugging surfaces runbook |
| `docs/testing.md` | testing, coverage ratchet, CI gates |
| `docs/tooling.md` | tooling setup |
| `docs/deployment-aws.md` | AWS deployment runbook |

## AWS Guidance

Any agent working with AWS in this project follows the guidance below, plus the project's own AWS knowledge: the runbook in [docs/deployment-aws.md](docs/deployment-aws.md) and the infrastructure-as-code under [infra/aws/](infra/aws/).

- The AWS MCP Server is wired in `.agents/mcp.json` (SigV4 via `aws login` credentials; see [docs/tooling.md](docs/tooling.md)); prefer its tools, and use `retrieve_skill` for any AWS skill instead of guessing. The `creating-secrets-using-best-practices` skill is pre-installed in `.agents/skills/`.

- Prefer the AWS MCP Server for AWS interactions — it provides sandboxed execution, observability, and audit logging. If unavailable, use the AWS CLI directly.
- Before starting a task, check whether a relevant AWS skill is available. Load the skill with `retrieve_skill` and prefer its guidance over general knowledge.
- When uncertain about specific AWS details (API parameters, permissions, limits, error codes), verify against documentation rather than guessing. State uncertainty explicitly if you cannot confirm.
- When creating infrastructure, prefer infrastructure-as-code (AWS CDK or CloudFormation) over direct CLI commands.
- When working with infrastructure, follow AWS Well-Architected Framework principles.
- Do not use em dashes in AWS resource names or descriptions. Use hyphens instead.

### Secret Safety

- MUST load the `aws-secrets-manager` skill first for any secret, credential, API key, token, or password task. MUST NOT call `secretsmanager get-secret-value` or `batch-get-secret-value`, and MUST NOT hit the Secrets Manager Agent daemon directly. MUST use `{{resolve:secretsmanager:secret-id:SecretString:json-key}}` with `asm-exec` so the secret resolves at runtime without entering context.

> Note: in this repository AWS secrets live in **SSM Parameter Store** (`SecureString`), not Secrets Manager — see the password-rotation and secret-handling runbook in [docs/deployment-aws.md](docs/deployment-aws.md).

## Code Standards
### Testing coverage
- Unit Testing coverage must include all corner cases
- Unit Testing code should be as concise and easy to understand as possible without requiring inline comments
- Testing project uses xUnit, Moq and FluentAssertions, make full use of their capabilities

### Documentation
- **XML comments** required for: Tests, Classes, Properties, Methods
- XML comments should include, as applicable and needed for clarity: `<summary>`, `<remarks>`, `<param>`, `<paramref>`, `<exception>`, `<returns>`, `<value>`, `<seealso>`, `<inheritdoc>`, `<see>`, `<seealso>` and `cref`, `href`, `name`, `type`, `path` etc.
- `<summary>` comments must explain **what** the commented code does into greater detail than merely it's name.
- `<remarks>` comments must explain **why** it's implemented in this way and/or from which business rule this comes from.
- **Inline comments**: Minimize. Avoid if a logging statement conveys equivalent info.

### Logging
- Use **Serilog** or `Console.WriteLine` with explicit levels:
  - `Information`: Normal operations, progress
  - `Warning`: Recoverable edge cases
  - `Error`: Failures requiring investigation

<!-- BEGIN OPENLORE (managed — edits inside this block will be overwritten) -->
<!-- openlore-fingerprint: 25cdd746ebf39b56 -->
This project uses OpenLore for persistent architectural memory.

ALWAYS call `orient()` (via the openlore MCP server, or `npx openlore orient --json`)
before reading source files when starting a new task. This returns the relevant
functions, callers, spec sections, and insertion points for the task at hand —
one structural lookup instead of file-by-file rediscovery.

OpenLore prefixes tool responses with a brief, factual freshness note (the
Epistemic Lease) once your cached context has aged or the repo has moved since
your last `orient()`. It is informational — re-`orient()` if you are relying on
cached cross-module structure; otherwise carry on.

For the MCP setup, ensure `openlore mcp` is configured as an MCP server.
See https://github.com/clay-good/OpenLore for details.
<!-- END OPENLORE -->
