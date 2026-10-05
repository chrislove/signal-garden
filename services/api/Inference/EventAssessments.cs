using System.Collections.Concurrent;
using SignalGarden.Core.Abstractions;
using SignalGarden.Core.Models;

namespace SignalGarden.Api.Inference;

/// <summary>
/// Attaches assessments to a scan's events without letting the model slow the
/// dashboard down or run up a bill.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Each event is assessed once and cached by its id (events persist across
/// many 20 s polls, and several viewers poll at once). The cached item is the
/// in-flight task, so concurrent requests share one call.</item>
/// <item>A request waits at most <see cref="Budget"/> for assessments; slower ones
/// keep running and appear on the next poll.</item>
/// <item>Failures are cached briefly, then retried, and never break the scan.</item>
/// </list>
/// </remarks>
public sealed class EventAssessments(IEventAssessor assessor, ILogger<EventAssessments> logger)
{
    public static readonly TimeSpan Budget = TimeSpan.FromSeconds(2.5);
    private static readonly TimeSpan Keep = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan RetryFailuresAfter = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<string, Entry> _cache = new();

    private sealed record Entry(Task<Assessment?> Task, DateTimeOffset Created);

    public async Task<IReadOnlyList<OperationalEvent>> AttachAsync(IReadOnlyList<OperationalEvent> events)
    {
        Evict();

        var pending = events.Select(e => (Event: e, Task: GetOrStart(e))).ToList();
        await Task.WhenAny(Task.WhenAll(pending.Select(p => p.Task)), Task.Delay(Budget));

        return pending
            .Select(p => p.Task.IsCompletedSuccessfully && p.Task.Result is { } a ? p.Event with { Assessment = a } : p.Event)
            .ToList();
    }

    private Task<Assessment?> GetOrStart(OperationalEvent e) =>
        _cache.GetOrAdd(e.Id, _ => new Entry(AssessSafelyAsync(e), DateTimeOffset.UtcNow)).Task;

    private async Task<Assessment?> AssessSafelyAsync(OperationalEvent e)
    {
        try
        {
            // Not tied to the HTTP request: if the viewer leaves, the answer is
            // still worth caching for the next one.
            return await assessor.AssessAsync(e, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Assessment failed for {EventId}", e.Id);
            throw;
        }
    }

    private void Evict()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (id, entry) in _cache)
        {
            var age = now - entry.Created;
            if (age > Keep || (entry.Task.IsFaulted && age > RetryFailuresAfter))
                _cache.TryRemove(id, out _);
        }
    }
}
