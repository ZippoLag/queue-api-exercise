namespace AuthDbInit;

/// <summary>
/// The command-line front-end of the credential-store initializer tool.
/// </summary>
/// <remarks>
/// Hoisted out of <c>Program.cs</c> top-level statements so the arg-parsing and outcome logic is unit
/// testable; <c>Program.cs</c> remains a thin shim forwarding <c>Console</c> streams and returning the
/// exit code. The exit-code contract mirrors the script's usage:
/// <c>0</c> when the store was initialized or already seeded, <c>1</c> on a usage error.
/// </remarks>
public static class Cli
{
    /// <summary>
    /// Runs the tool with the given arguments and writes progress/errors to the supplied writers.
    /// </summary>
    /// <param name="args">The command-line arguments: <c>&lt;db-path&gt; &lt;username&gt; &lt;password&gt;</c>.</param>
    /// <param name="stdout">The stream for informational progress output.</param>
    /// <param name="stderr">The stream for error/usage output.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The process exit code: <c>0</c> on success, <c>1</c> when required arguments are missing.</returns>
    public static async Task<int> RunAsync(
        string[] args,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken cancellationToken = default)
    {
        var dbPath = args.ElementAtOrDefault(0);
        var username = args.ElementAtOrDefault(1);
        var password = args.ElementAtOrDefault(2);

        if (string.IsNullOrWhiteSpace(dbPath) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await stderr.WriteLineAsync("[Error] Usage: dotnet run --project tools/AuthDbInit -- <db-path> <username> <password>");
            return 1;
        }

        // A full connection string (contains '=') is passed through untouched; a bare path is wrapped so
        // callers can hand either form to the tool.
        var connectionString = dbPath.Contains('=') ? dbPath : $"Data Source={dbPath}";

        var result = await AuthDbInitializer.InitializeAsync(connectionString, username, password, cancellationToken);
        if (result == InitializationResult.AlreadyExists)
        {
            await stdout.WriteLineAsync($"[Warning] User '{username}' already exists in '{dbPath}'; leaving it unchanged.");
        }
        else
        {
            await stdout.WriteLineAsync($"[Information] Created user '{username}' in '{dbPath}'.");
        }

        return 0;
    }
}
