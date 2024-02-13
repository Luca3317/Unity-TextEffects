using IntervalTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

public interface ITagCacher<T> where T : ITagWrapper
{
    public T CacheTag(EffectTag tag, EffectTagIndices indices);
}

public class CachedCollection<T> : IEnumerable<T> where T : ITagWrapper
{
    private Dictionary<int, MinMax> minMax = new();

    private List<T> cache = new List<T>();
    private ObservableTagCollection tagCollection;
    private ITagCacher<T> cacher;

    private int max = int.MinValue;
    private int min = int.MaxValue;

    public int Count => cache.Count;

    public CachedCollection(ITagCacher<T> cacher, ObservableTagCollection tagCollection)
    {
        this.cacher = cacher;
        this.tagCollection = tagCollection;

        int counter = 0;
        foreach (var tagData in tagCollection)
        {
            Add(counter++, cacher.CacheTag(tagData.Tag, tagData.Indices));
        }

        tagCollection.CollectionChanged += OnCollectionChanged;
    }

    private void Add(int cachedIndex, T tuple)
    {
        foreach (var kvp in minMax)
        {
            if (kvp.Value.MinIndex >= cachedIndex)
            {
                kvp.Value.MinIndex += 1;
            }
            if (kvp.Value.MaxIndex >= cachedIndex)
            {
                kvp.Value.MaxIndex += 1;
            }
        }

        for (int index = tuple.Indices.StartIndex; index < tuple.Indices.EndIndex; index++)
        {
            if (!minMax.TryGetValue(index, out MinMax mm))
            {
                minMax.Add(index, new MinMax(cachedIndex));
                continue;
            }

            if (mm.MaxIndex < cachedIndex)
            {
                mm.MaxIndex = cachedIndex;
            }
            if (mm.MinIndex > cachedIndex)
            {
                mm.MinIndex = cachedIndex;
            }
        }

        if (tuple.Indices.EndIndex > max) max = tuple.Indices.EndIndex;
        if (tuple.Indices.StartIndex < min) min = tuple.Indices.StartIndex;

        cache.Insert(cachedIndex, tuple);
    }

    private void Remove(int cachedIndex)
    {
        T tuple = cache[cachedIndex];

        for (int index = tuple.Indices.StartIndex; index < tuple.Indices.EndIndex; index++)
        {
            MinMax mm = minMax[index];

            // If removing the tag that serves as min tag for the current index
            if (mm.MinIndex == cachedIndex)
            {
                // If this tag also serves as max tag => is the only tag for the current index
                if (mm.MaxIndex == cachedIndex)
                {
                    minMax.Remove(index);
                    continue;
                }

                bool found = false;

                // Find new min tag
                for (int i = mm.MinIndex + 1; i <= mm.MaxIndex; i++)
                {
                    if (cache[i].Indices.Contains(index))
                    {
                        mm.MinIndex = i;
                        found = true;
                        break;
                    }
                }

                if (!found) Debug.LogError("Failed to find new min tag; BUG");

            }
            else if (mm.MaxIndex == cachedIndex)
            {
                // Find new max tag

                bool found = false;

                for (int i = mm.MaxIndex - 1; i >= mm.MinIndex; i--)
                {
                    if (cache[i].Indices.Contains(index))
                    {
                        mm.MaxIndex = i;
                        found = true;
                        break;
                    }
                }

                if (!found) Debug.LogError("Failed to find new max tag; BUG");
            }
        }

        foreach (var kvp in minMax)
        {
            if (kvp.Value.MinIndex > cachedIndex)
            {
                kvp.Value.MinIndex -= 1;
            }
            if (kvp.Value.MaxIndex > cachedIndex)
            {
                kvp.Value.MaxIndex -= 1;
            }
        }

        cache.RemoveAt(cachedIndex);

        // Update max/min
        min = cache[0].Indices.StartIndex;
        if (tuple.Indices.EndIndex == max)
        {
            max = int.MinValue;
            foreach (var i in cache)
            {
                if (i.Indices.EndIndex > max)
                    max = i.Indices.EndIndex;
            }
        }
    }

    private void Set(int cachedIndex, T tuple)
    {
        Remove(cachedIndex);
        Add(cachedIndex, tuple);
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems.Count > 1) Debug.LogWarning("Added more than one element; Should be impossible");
                var tuple = (EffectTagTuple)e.NewItems[0];
                Add(e.NewStartingIndex, cacher.CacheTag(tuple.Tag, tuple.Indices));
                break;

            case NotifyCollectionChangedAction.Remove:
                int index = e.OldStartingIndex;
                for (int i = 0; i < e.OldItems.Count; i++)
                {
                    Remove(index);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                cache.TrimExcess();
                minMax.TrimExcess();
                cache.Clear();
                minMax.Clear();
                break;

            case NotifyCollectionChangedAction.Move:
                throw new NotImplementedException();

            case NotifyCollectionChangedAction.Replace:
                index = e.NewStartingIndex;

                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    tuple = (EffectTagTuple)e.NewItems[i];
                    Set(index + i, cacher.CacheTag(tuple.Tag, tuple.Indices));
                }
                break;
        }
    }

    public class MinMax
    {
        public int MaxIndex;
        public int MinIndex;

        public MinMax(int index)
        {
            MaxIndex = index;
            MinIndex = index;
        }
    }

    public MinMax MinMaxAt(int index)
    {
        if (!minMax.TryGetValue(index, out var mm))
        {
            return null;
        }
        return mm;
    }

    public T this[int index]
    {
        get => cache[index];
    } 

    public bool HasAny() => cache.Count > 0;
    public bool HasAnyContaining(int index)
    {
        if (index < min) return false;
        if (index > max) return false;
        return minMax.ContainsKey(index);
    }
    public bool HasAnyAt(int index)
    {
        if (!minMax.TryGetValue(index, out MinMax mm))
        {
            return false;
        }

        for (int i = mm.MinIndex; i <= mm.MaxIndex; i++)
        {
            if (cache[i].Indices.StartIndex == index) return true;
        }

        return false;
    }

    public IEnumerable<T> GetContaining(int index)
    {
        if (!minMax.TryGetValue(index, out MinMax mm))
        {
            yield break;
        }

        for (int i = mm.MinIndex; i <= mm.MaxIndex; i++)
        {
            T cached = cache[i];

            if (cached.Indices.StartIndex > index) yield break;

            if (cached.Indices.Contains(index)) yield return cached;
        }
    }

    public IEnumerable<T> GetAt(int startIndex)
    {
        if (!minMax.TryGetValue(startIndex, out MinMax mm))
        {
            yield break;
        }

        for (int i = mm.MinIndex; i <= mm.MaxIndex; i++)
        {
            T cached = cache[i];

            if (cached.Indices.StartIndex > startIndex)
            {
                yield break;
            }

            if (cached.Indices.StartIndex == startIndex)
            {
                yield return cached;
            }
        }
    }

    public StructContainingEnumerable GetContaining_NonAlloc(int index)
    {
        if (!minMax.TryGetValue(index, out MinMax mm))
        {
            return new StructContainingEnumerable(null, 0, 0, 0);
        }

        return new StructContainingEnumerable(cache, index, mm.MaxIndex, mm.MinIndex);
    }

    public StructReversedContainingEnumerable GetContainingReversed_NonAlloc(int index)
    {
        if (!minMax.TryGetValue(index, out MinMax mm))
        {
            return new StructReversedContainingEnumerable(null, 0, 0, 0);
        }

        return new StructReversedContainingEnumerable(cache, index, mm.MaxIndex, mm.MinIndex);
    }

    public struct StructReversedContainingEnumerable
    {
        private readonly List<T> pool;
        private int containedIndex;
        private int minIndex;
        private int maxIndex;

        public StructReversedContainingEnumerable(List<T> pool, int containedIndex, int maxIndex, int minIndex)
        {
            this.pool = pool;
            this.containedIndex = containedIndex;
            this.minIndex = minIndex;
            this.maxIndex = maxIndex;
        }

        public StructReversedContainingEnumerator GetEnumerator()
        {
            return new StructReversedContainingEnumerator(this.pool, containedIndex, maxIndex, minIndex);
        }
    }

    public struct StructContainingEnumerable
    {
        private readonly List<T> pool;
        private int containedIndex;
        private int minIndex;
        private int maxIndex;

        public StructContainingEnumerable(List<T> pool, int containedIndex, int maxIndex, int minIndex)
        {
            this.pool = pool;
            this.containedIndex = containedIndex;
            this.minIndex = minIndex;
            this.maxIndex = maxIndex;
        }

        public StructContainingEnumerator GetEnumerator()
        {
            return new StructContainingEnumerator(this.pool, containedIndex, maxIndex, minIndex);
        }
    }

    public struct StructReversedContainingEnumerator
    {
        private readonly List<T> pool;
        private readonly int containedIndex;
        private readonly int maxIndex;
        private readonly int minIndex;
        private int index;

        internal StructReversedContainingEnumerator(List<T> pool, int containedIndex, int maxIndex, int minIndex)
        {
            this.pool = pool;
            this.containedIndex = containedIndex;
            this.index = maxIndex + 1;
            this.maxIndex = maxIndex;
            this.minIndex = minIndex;
        }

        public T Current
        {
            get
            {
                return this.pool[this.index];
            }
        }

        public bool MoveNext()
        {
            if (pool == null) return false;
            while (--index >= minIndex && !pool[index].Indices.Contains(containedIndex)) { }
            return this.minIndex <= this.index;
        }

        public void Reset()
        {
            this.index = maxIndex + 1;
        }
    }

    public struct StructContainingEnumerator
    {
        private readonly List<T> pool;
        private readonly int containedIndex;
        private readonly int maxIndex;
        private readonly int minIndex;
        private int index;

        internal StructContainingEnumerator(List<T> pool, int containedIndex, int maxIndex, int minIndex)
        {
            this.pool = pool;
            this.containedIndex = containedIndex;
            this.index = minIndex - 1;
            this.maxIndex = maxIndex;
            this.minIndex = minIndex;
        }

        public T Current
        {
            get
            {
                return this.pool[this.index];
            }
        }

        public bool MoveNext()
        {
            if (pool == null) return false;
            while (++index <= maxIndex && !pool[index].Indices.Contains(containedIndex)) { }
            return this.maxIndex >= this.index;
        }

        public void Reset()
        {
            this.index = minIndex - 1;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return cache.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return cache.GetEnumerator();
    }
}
