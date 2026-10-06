#nullable enable
using System;
using System.Collections.Generic;

namespace TotalFog.Core;

/// <summary>Ordered unique listeners, with no list allocation for an empty or singleton cell.</summary>
public struct ListenerCell<T> where T : class
{
    private T? first;
    private List<T>? additional;
    public int Count => first == null ? 0 : 1 + (additional?.Count ?? 0);
    public T this[int index]
    {
        get
        {
            if ((uint)index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return index == 0 ? first! : additional![index - 1];
        }
    }
    public bool Contains(T? item) => first != null && item != null &&
        (EqualityComparer<T>.Default.Equals(first, item) || additional?.Contains(item) == true);
    public bool Add(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (first == null) { first = item; return true; }
        if (Contains(item)) return false;
        (additional ??= new List<T>()).Add(item);
        return true;
    }
    public bool Remove(T? item)
    {
        if (first == null || item == null) return false;
        if (EqualityComparer<T>.Default.Equals(first, item))
        {
            if (additional == null) first = null;
            else
            {
                first = additional[0]; additional.RemoveAt(0);
                if (additional.Count == 0) additional = null;
            }
            return true;
        }
        if (additional == null || !additional.Remove(item)) return false;
        if (additional.Count == 0) additional = null;
        return true;
    }
}
