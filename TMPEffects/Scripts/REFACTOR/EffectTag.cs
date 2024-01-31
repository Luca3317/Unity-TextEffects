using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

public sealed class EffectTag
{
    public string Name => name;
    public char Prefix => prefix;
    public ReadOnlyDictionary<string, string> Parameters => parameters;

    private readonly string name;
    private readonly char prefix;
    private readonly ReadOnlyDictionary<string, string> parameters;

    public EffectTag(string name, char prefix, IDictionary<string, string> parameters)
    {
        this.name = name;
        this.prefix = prefix;
        if (parameters == null)
            this.parameters = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        else
            this.parameters = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(parameters));
    }
}

public struct EffectTagIndices : IComparable<EffectTagIndices>
{
    public int StartIndex => startIndex;
    public int EndIndex => endIndex;
    public int OrderAtIndex => orderAtIndex;

    public bool IsOpen => endIndex == -1;
    public int Length => IsOpen ? endIndex : endIndex - startIndex + 1;

    private int startIndex;
    private int endIndex;
    private int orderAtIndex;

    public EffectTagIndices(int startIndex, int endIndex, int orderAtIndex)
    {
        // TODO Make endindex = -1 representing open some constant or smth; also used ie in tagcollection and animator
        if (startIndex < 0) throw new ArgumentOutOfRangeException(nameof(startIndex));
        if (endIndex < -1) throw new ArgumentOutOfRangeException(nameof(endIndex));

        this.startIndex = startIndex;
        this.endIndex = endIndex;
        this.orderAtIndex = orderAtIndex;
    }

    public int CompareTo(EffectTagIndices other)
    {
        int res = startIndex.CompareTo(other.startIndex);
        if (res == 0) return orderAtIndex.CompareTo(other.orderAtIndex);
        return res;
    }
}