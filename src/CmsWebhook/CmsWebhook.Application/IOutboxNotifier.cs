namespace CmsWebhook.Application;

/// <summary>
/// Signals the outbox worker that new events are available for immediate processing.
/// </summary>
/// <remarks>
/// Keeps the Application layer free of any in-process plumbing: the command handler only knows that a
/// notification exists, and the Infrastructure layer implements it with a <c>System.Threading.Channels</c>
/// fast-path (design D2). The durable delivery guarantee does not depend on this signal — the worker's
/// startup/periodic sweeps re-process pending rows regardless.
/// </remarks>
public interface IOutboxNotifier
{
    /// <summary>
    /// Notifies the worker that there may be pending events to process.
    /// </summary>
    void Notify();
}
