using System;
using System.Collections.Generic;

namespace AutoNumber.Extensions;

internal static class DictionaryExtensions
{
    internal static TValue GetValue<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        object dictionaryLock,
        Func<TValue> valueInitializer)
    {
        var found = dictionary.TryGetValue(key, out var value);
        if (found)
        {
            return value;
        }

        lock (dictionaryLock)
        {
            found = dictionary.TryGetValue(key, out value);
            if (found)
            {
                return value;
            }

            value = valueInitializer();

            dictionary.Add(key, value);
        }

        return value;
    }
}