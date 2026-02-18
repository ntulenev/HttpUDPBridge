using System.Collections.Concurrent;

using Abstractions;

using Models;

namespace Services;

/// <summary>
/// Provides a thread-safe in-memory registry of pending bridge requests.
/// </summary>
public sealed class RequestRegistry : IRequestRegistry
{
    /// <inheritdoc />
    public PendingRequestRegistration Register(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        while (true)
        {
            if (_pendingRequests.TryGetValue(requestId, out var existingState))
            {
                _ = existingState.IncrementWaiters();
                return new PendingRequestRegistration(
                    requestId,
                    existingState.Completion.Task,
                    false);
            }

            var createdState = new PendingRequestState();
            if (_pendingRequests.TryAdd(requestId, createdState))
            {
                return new PendingRequestRegistration(
                    requestId,
                    createdState.Completion.Task,
                    true);
            }
        }
    }

    /// <inheritdoc />
    public bool TryCompleteWithResponse(string requestId, CachedUdpResponse response)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(response);

        if (!_pendingRequests.TryGetValue(requestId, out var state))
        {
            return false;
        }

        var completed = state.Completion.TrySetResult(
            PendingUdpRequestResult.WithResponse(response));

        RemoveIfCompletedWithoutWaiters(requestId, state);
        return completed;
    }

    /// <inheritdoc />
    public bool TryCompleteWithoutResponse(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        if (!_pendingRequests.TryGetValue(requestId, out var state))
        {
            return false;
        }

        var completed = state.Completion.TrySetResult(
            PendingUdpRequestResult.NoResponse);

        RemoveIfCompletedWithoutWaiters(requestId, state);
        return completed;
    }

    /// <inheritdoc />
    public void Release(string requestId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        if (!_pendingRequests.TryGetValue(requestId, out var state))
        {
            return;
        }

        _ = state.DecrementWaiters();
        RemoveIfCompletedWithoutWaiters(requestId, state);
    }

    private void RemoveIfCompletedWithoutWaiters(
        string requestId,
        PendingRequestState state)
    {
        if (!state.Completion.Task.IsCompleted || state.Waiters > 0)
        {
            return;
        }

        _ = _pendingRequests.TryRemove(
            new KeyValuePair<string, PendingRequestState>(requestId, state));
    }

    private readonly ConcurrentDictionary<string, PendingRequestState> _pendingRequests =
        new(StringComparer.Ordinal);

    private sealed class PendingRequestState
    {
        public PendingRequestState()
        {
            Completion = new TaskCompletionSource<PendingUdpRequestResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters = 1;
        }

        public TaskCompletionSource<PendingUdpRequestResult> Completion { get; }

        public int Waiters => Volatile.Read(ref _waiters);

        public int IncrementWaiters() => Interlocked.Increment(ref _waiters);

        public int DecrementWaiters() => Interlocked.Decrement(ref _waiters);

        private int _waiters;
    }
}
