using CmsWebhook.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CmsWebhook.Infrastructure;

/// <summary>
/// Background service that processes recorded events from the outbox asynchronously.
/// </summary>
/// <remarks>
/// Design D2: the worker is the processing side of the in-process outbox. On startup it sweeps pending
/// rows (recovering events left behind by a crash or restart), then loops waiting for either the in-process
/// channel signal (immediate processing) or a periodic sweep timer (durability safety net). Each sweep
/// processes pending events in FIFO order, one scoped unit of work per event, so a failing event cannot
/// corrupt the next one (spec: "Events are processed asynchronously", "A failing event is marked failed
/// and processing continues").
/// </remarks>
public class CmsEventProcessorWorker : BackgroundService
{
    /// <summary>
    /// How often the worker re-scans the outbox for pending rows when no signal arrives.
    /// </summary>
    public static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    private readonly OutboxChannel _outbox;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CmsEventProcessorWorker> _logger;

    /// <summary>
    /// Creates the worker.
    /// </summary>
    /// <param name="outbox">The in-process notification channel.</param>
    /// <param name="scopeFactory">The scope factory used to resolve scoped services per sweep.</param>
    /// <param name="logger">The logger.</param>
    public CmsEventProcessorWorker(
        OutboxChannel outbox,
        IServiceScopeFactory scopeFactory,
        ILogger<CmsEventProcessorWorker> logger)
    {
        _outbox = outbox;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("CMS event processor worker started.");
        await ProcessPendingAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var signal = _outbox.Reader.WaitToReadAsync(stoppingToken).AsTask();
            var timer = Task.Delay(SweepInterval, stoppingToken);
            await Task.WhenAny(signal, timer);

            try
            {
                await ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Outbox sweep failed; it will be retried on the next cycle.");
            }
        }

        _logger.LogInformation("CMS event processor worker stopped.");
    }

    /// <summary>
    /// Processes every pending event, each in its own scoped unit of work.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    internal async Task ProcessPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ICmsEventLogRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<ICmsEventProcessor>();

        var pending = await repository.GetPendingAsync(cancellationToken);
        foreach (var @event in pending)
        {
            try
            {
                await processor.ProcessAsync(@event, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Event {EventId} could not be processed and will stay pending.", @event.Id);
            }
        }
    }
}
