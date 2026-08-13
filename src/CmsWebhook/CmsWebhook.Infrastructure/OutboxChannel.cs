using System.Threading.Channels;
using CmsWebhook.Application;

namespace CmsWebhook.Infrastructure;

/// <summary>
/// In-process notification fast-path between the ingest command and the outbox worker.
/// </summary>
/// <remarks>
/// Design D2: an unbounded <see cref="Channel{T}"/> wakes the worker immediately after events are
/// recorded. The channel is only a signal — the value is unused — because the database is the durable
/// queue of record: the worker's startup/periodic sweeps re-process any pending rows regardless of
/// whether the signal was lost (e.g. a crash between commit and notify).
/// </remarks>
public sealed class OutboxChannel : IOutboxNotifier
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>();

    /// <summary>
    /// The reader the outbox worker drains to learn that work is available.
    /// </summary>
    internal ChannelReader<long> Reader => _channel.Reader;

    /// <inheritdoc/>
    public void Notify() => _channel.Writer.TryWrite(0);
}
