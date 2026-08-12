## Context

AGENTS.md requires XML documentation (including `<exception>` tags where applicable) for classes, properties, and methods. Today three members explain thrown exceptions in prose instead: `IUserCredentialsProvider.VerifyCredentialsAsync`/`UserExistsAsync` (the store-unavailable failure is only implied in class `<remarks>`), `AuthDbInitializer.InitializeAsync` (no exception tags at all), and the `Program.cs` helper methods (plain `//` comments, including a literal "Throws InvalidOperationException when…" line). `Pbkdf2PasswordHasher` is already compliant. See proposal.md — Why for motivation.

## Goals / Non-Goals

**Goals:**
- Every public member that can throw documents it with `<exception cref="…">`, moving the "what can go wrong" out of prose.
- Keep `<remarks>` for the *why* (business rules, design decisions D2/D6/D7) rather than exception descriptions.
- Build stays at 0 warnings; all tests stay green; no behavior or API changes.

**Non-Goals:**
- No changes to tests, behavior, or signatures.
- No exception tags on members that cannot throw (e.g. `FindRepositoryRoot` returns `null`, `Pbkdf2PasswordHasher.Verify` returns `false`).

## Decisions

### D1: Document exceptions on the interface, inherit everywhere else
`IUserCredentialsProvider.VerifyCredentialsAsync` and `UserExistsAsync` get `<exception cref="InvalidOperationException">` (the store-unavailable failure wrapped by `DbUserCredentialsProvider`, design D7 of `add-sqlite-auth-db`). The concrete provider's `<inheritdoc/>` inherits them, and the in-memory test provider inherits harmlessly.
- *Alternative considered*: tagging only the concrete `DbUserCredentialsProvider` — rejected: the seam contract belongs on the interface, and the class `<remarks>` keeps explaining *why* the wrapping exists.

### D2: `AuthDbInitializer.InitializeAsync` tags `DbException`
`EnsureCreatedAsync`/`SaveChangesAsync` surface provider connection failures as `System.Data.Common.DbException` — the ADO.NET base class, so the tag stays correct if the SQLite provider is swapped (consistent with the provider-agnostic wrapping in `DbUserCredentialsProvider`).

### D3: `Program.cs` helpers get real XML doc blocks
`ResolveConnectionString`, `FindRepositoryRoot`, and `ResolveCmsUsername` live inside the top-level `Program.cs` as *static local functions*, where XML doc comments are not valid language elements (CS1587). They are therefore hoisted into the existing `public partial class Program` as `private static` methods — the same class integration tests already target via `WebApplicationFactory<Program>` — so the tags can attach directly. Their prose `//` comments become `<summary>/<param>/<returns>` blocks; the two that throw tag `<exception cref="InvalidOperationException">` (missing `ConnectionStrings:AuthDb`, invalid username length); `FindRepositoryRoot` documents its `null` return instead.

### D4: Hasher left as-is
`Hash` already tags `ArgumentNullException`; `Verify` never throws (nulls and malformed hashes return `false`). No change.

## Risks / Trade-offs

- [Over-documenting exceptions on methods that rarely throw] → Tags are added only where a throw is real, user-actionable, and part of the contract; everything else is documented as a return value.
- [Interface-level tags nominally apply to the non-throwing in-memory test provider] → Accepted: the tag documents the seam's contract; the test provider never throws.

## Migration Plan

None — documentation-only. Verification: `dotnet build` reports 0 warnings (missing-XML-doc and tag-shape errors surface here), `dotnet test` stays green, and a grep confirms no prose "Throws …" lines remain in the touched files.

## Open Questions

None.
