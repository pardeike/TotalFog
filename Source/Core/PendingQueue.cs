using System;
using System.Collections.Generic;

namespace TotalFog.Core;

/// <summary>Payload ownership without polling, game types, or target-based deduplication.</summary>
public sealed class PendingQueue<T>
{
    public List<T> Items = new();

    public void Add(T payload)
    {
        if (!Items.Contains(payload))
            Items.Add(payload);
    }

    public void Drain(
        Func<T, bool> valid,
        Func<T, bool> ready,
        Action<T> replay,
        Action<T, Exception> failed = null
    )
    {
        int remaining = Items.Count;
        for (int i = 0; i < remaining; )
        {
            var item = Items[i];
            try
            {
                if (valid(item))
                {
                    if (!ready(item))
                    {
                        i++;
                        continue;
                    }
                    replay(item);
                }
            }
            catch (Exception error)
            {
                failed?.Invoke(item, error);
            }
            Items.RemoveAt(i);
            remaining--;
        }
    }
}
