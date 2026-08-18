using Microsoft.EntityFrameworkCore;

namespace QueueApi.Persistence;

/// <summary>
/// EF Core provider-selection helpers for configuration-driven database engines.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the EF Core provider selected by the given provider name.
    /// </summary>
    /// <param name="builder">The options builder to configure.</param>
    /// <param name="provider">The provider name, compared case-insensitively (e.g. <c>sqlite</c>).</param>
    /// <param name="connectionString">The connection string for the selected provider.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="provider"/> is not one of the supported providers.
    /// </exception>
    /// <remarks>
    /// This is the single place in the codebase where a configured provider name maps to an EF Core provider
    /// call (design D1 of change configurable-db-provider): adding PostgreSQL later is one new switch branch
    /// plus the <c>Npgsql.EntityFrameworkCore.PostgreSQL</c> package reference, with no registration call-site
    /// change. An unknown value fails fast instead of silently falling back, because a deployment pointed at
    /// the wrong engine would corrupt its data contract (design D2).
    /// </remarks>
    public static DbContextOptionsBuilder UseConfiguredProvider(
        this DbContextOptionsBuilder builder,
        string provider,
        string connectionString)
    {
        switch (provider?.Trim().ToLowerInvariant())
        {
            case "sqlite":
                return builder.UseSqlite(connectionString);
            default:
                throw new InvalidOperationException(
                    $"Unsupported database provider '{provider}'. Supported providers: sqlite.");
        }
    }

    /// <summary>
    /// Configures the EF Core provider selected by the given provider name, preserving the strongly-typed builder.
    /// </summary>
    /// <param name="builder">The options builder to configure.</param>
    /// <param name="provider">The provider name, compared case-insensitively (e.g. <c>sqlite</c>).</param>
    /// <param name="connectionString">The connection string for the selected provider.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="provider"/> is not one of the supported providers.
    /// </exception>
    /// <remarks>
    /// The typed overload exists so callers that construct a <c>DbContextOptions&lt;TContext&gt;</c> directly
    /// (the <c>AuthDbInit</c> tool, design D6) can route through the same switch; it mirrors how the standard
    /// EF Core providers ship both the builder and typed-builder forms.
    /// </remarks>
    public static DbContextOptionsBuilder<TContext> UseConfiguredProvider<TContext>(
        this DbContextOptionsBuilder<TContext> builder,
        string provider,
        string connectionString)
        where TContext : DbContext
    {
        // The cast to the non-generic builder forces the non-generic overload below; without it, extension
        // overload resolution would re-select this generic overload (the more specific receiver type) and
        // recurse until the stack overflows.
        ((DbContextOptionsBuilder)builder).UseConfiguredProvider(provider, connectionString);
        return builder;
    }
}
