using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using UnityEngine;

namespace TMPEffects.Tags
{
    public class EffectTagComparer : IComparer<EffectTag>
    {
        public int Compare(EffectTag x, EffectTag y)
        {
            int res = x.StartIndex.CompareTo(y.StartIndex);
            if (res == 0) return x.OrderAtIndex.CompareTo(y.OrderAtIndex);
            return res;
        }
    }

    public sealed class EffectTag
    {
        public EffectTagData Data => data;
        public EffectTagIndices Indices => indices;

        public int StartIndex => indices.StartIndex;
        public int EndIndex => indices.EndIndex;
        public int OrderAtIndex => indices.OrderAtIndex;

        public bool IsOpen => indices.IsOpen;
        public int Length => indices.Length;

        public string Name => data.Name;
        public char Prefix => data.Prefix;
        public ReadOnlyDictionary<string, string> Parameters => data.Parameters;

        private readonly EffectTagData data;
        private readonly EffectTagIndices indices;

        public EffectTag(EffectTagData data, EffectTagIndices indices)
        {
            this.data = data;
            this.indices = indices;
        }

        internal void SetStartIndex(int newIndex) => indices.SetStartIndex(newIndex);
        internal void SetEndIndex(int newIndex) => indices.SetEndIndex(newIndex);
        internal void SetOrderAtIndex(int newIndex) => indices.SetOrderAtIndex(newIndex);
    }

    public sealed class EffectTagData
    {
        public string Name => name;
        public char Prefix => prefix;
        public ReadOnlyDictionary<string, string> Parameters => parameters;

        private readonly string name;
        private readonly char prefix;
        private readonly ReadOnlyDictionary<string, string> parameters;

        public EffectTagData(string name, char prefix, IDictionary<string, string> parameters)
        {
            this.name = name;
            this.prefix = prefix;
            this.parameters = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(parameters));
        }
    }

    public sealed class EffectTagIndices
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

        internal void SetStartIndex(int newIndex) => startIndex = newIndex;
        internal void SetEndIndex(int newIndex) => endIndex = newIndex;
        internal void SetOrderAtIndex(int newIndex) => orderAtIndex = newIndex;
    }
}
