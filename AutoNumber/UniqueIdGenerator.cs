using System;
using System.Collections.Generic;
using System.Threading;
using AutoNumber.Exceptions;
using AutoNumber.Extensions;
using AutoNumber.Interfaces;
using AutoNumber.Options;
using Microsoft.Extensions.Options;

namespace AutoNumber;

/// <summary>
///     Generate a new incremental id regards the scope name
/// </summary>
public class UniqueIdGenerator : IUniqueIdGenerator
{
    private readonly IOptimisticDataStore _optimisticDataStore;
    private readonly IDictionary<string, ScopeState> states = new Dictionary<string, ScopeState>();
    private readonly object statesLock = new();
    private int maxWriteAttempts = 25;

    public int BatchSize { get; set; } = 100;

    public int MaxWriteAttempts
    {
        get => maxWriteAttempts;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), value,
                    "MaxWriteAttempts must be a positive number.");

            maxWriteAttempts = value;
        }
    }

    public UniqueIdGenerator(IOptimisticDataStore optimisticDataStore)
    {
        _optimisticDataStore = optimisticDataStore;
        optimisticDataStore.Initialize();
    }

    public UniqueIdGenerator(IOptimisticDataStore optimisticDataStore, IOptions<AutoNumberOptions> options)
        : this(optimisticDataStore)
    {
        BatchSize = options.Value.BatchSize;
        MaxWriteAttempts = options.Value.MaxWriteAttempts;
    }

    public UniqueIdGenerator(IOptimisticDataStore optimisticDataStore, AutoNumberOptions options)
        : this(optimisticDataStore)
    {
        BatchSize = options.BatchSize;
        MaxWriteAttempts = options.MaxWriteAttempts;
    }

    /// <summary>
    ///     Generate a new incremental id regards the scope name
    /// </summary>
    /// <param name="scopeName">Scope name</param>
    /// <returns>Next available id</returns>
    public long NextId(string scopeName)
    {
        var state = GetScopeState(scopeName);

        lock (state.IdGenerationLock)
        {
            if (state.LastId == state.HighestIdAvailableInBatch)
                UpdateFromSyncStore(scopeName, state);

            return Interlocked.Increment(ref state.LastId);
        }
    }

    private ScopeState GetScopeState(string scopeName) =>
        states.GetValue(scopeName, statesLock, () => new ScopeState());

    private void UpdateFromSyncStore(string scopeName, ScopeState state)
    {
        var writesAttempted = 0;

        while (writesAttempted < MaxWriteAttempts)
        {
            var autoNumberState = _optimisticDataStore.GetAutoNumberState(scopeName);
            var nextId = autoNumberState.NextAvailableNumber;

            state.LastId = nextId - 1;
            state.HighestIdAvailableInBatch = state.LastId + BatchSize;
            autoNumberState.NextAvailableNumber = state.HighestIdAvailableInBatch + 1;

            if (_optimisticDataStore.TryOptimisticWrite(autoNumberState))
            {
                return;
            }
            writesAttempted++;
        }

        throw new UniqueIdGenerationException(
            $"Failed to update the data store after {writesAttempted} attempts. This likely represents too much contention against the store. Increase the batch size to a value more appropriate to your generation load.");
    }
}