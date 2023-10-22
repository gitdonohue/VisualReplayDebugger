// (c) 2021 Charles Donohue
// This code is licensed under MIT license (see LICENSE file for details)

using System;
using System.Collections.Generic;

namespace SelectionSet;

public class SelectionGroup<T>
{
    public HashSet<T> SelectionSet { get; private set; } = new HashSet<T>();

    public event Action Changed;

    public void Add(T element)
    {
        if ( SelectionSet.Add(element) )
        {
            Changed?.Invoke();
        }
    }

    public void Remove(T element)
    {
        if ( SelectionSet.Remove(element) )
        {
            Changed?.Invoke();
        }
    }

    public void Set(T element)
    {
        SelectionSet.Clear();
        Add(element);
    }

    public void Set(IEnumerable<T> elements)
    {
        SelectionSet = new HashSet<T>(elements);
        Changed?.Invoke();
    }

    public void Clear()
    {
        SelectionSet.Clear();
        Changed?.Invoke();
    }

    public bool Contains(T e) => SelectionSet.Contains(e);

    public bool Empty => SelectionSet.Count == 0;
}
