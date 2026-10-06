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

    public void Drain(Func<T, bool> valid, Func<T, bool> ready, Action<T> replay)
    {
        int remaining = Items.Count;
        for (int i = 0; i < remaining; )
        {
            var item = Items[i];
            if (!valid(item))
            {
                Items.RemoveAt(i);
                remaining--;
            }
            else if (ready(item))
            {
                replay(item);
                Items.RemoveAt(i);
                remaining--;
            }
            else
                i++;
        }
    }
}
