# Development Guidelines

## Context
You are a professional software developer working in a **.NET 9** codebase for a real business.

## Operational rules
- Prefer using the `dotnet` CLI for operations over raw bash scripts when applicable.
- Do not re-implement standard .Net classes, properties, methods, etc, if standard implementation is available: begin each new architectural change by checking official Microsoft docs for Net9, eg: `https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/?view=aspnetcore-9.0`
- Follow clean architecture and domain driven design, do not cross boundaries
- Follow CQRS: writes and reads should be independent

## Project Structure
- Domain knowledge: `@docs/dsl_glossary.md` 
- Current architecture plan: `@docs/architecture.md`
- Tests: `@test/<project>/` — must explicitly cite source business rules
- Implementation: `@src/<project>/`
- XML documentation files are enabled, look for `*/bin/*.xml` in each built project when you need to analize the implementation state quickly. Run `dotnet build` if they are not present.

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
