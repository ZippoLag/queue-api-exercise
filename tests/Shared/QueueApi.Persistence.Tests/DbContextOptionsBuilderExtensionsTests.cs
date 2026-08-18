using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using QueueApi.Persistence;

namespace QueueApi.Persistence.Tests;

/// <summary>
/// Unit tests for <see cref="DbContextOptionsBuilderExtensions"/>.
/// </summary>
public class DbContextOptionsBuilderExtensionsTests
{
    /// <summary>
    /// An unknown provider value fails fast with an error naming the supported providers.
    /// </summary>
    /// <remarks>
    /// Source business rule: spec "Unknown provider fails fast" (change configurable-db-provider) — a
    /// deployment pointed at an unsupported engine must fail at startup instead of silently falling back,
    /// because it would otherwise corrupt its data contract. This is the one switch branch no boot-time
    /// integration test exercises (every boot configures the sqlite provider), so the 100% unique-line
    /// coverage ratchet requires it covered here.
    /// </remarks>
    [Fact]
    public void UseConfiguredProvider_WithUnknownProvider_ThrowsDescriptiveError()
    {
        var builder = new DbContextOptionsBuilder();

        var act = () => builder.UseConfiguredProvider("postgres", "Data Source=:memory:");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Supported providers: sqlite*");
    }
}
