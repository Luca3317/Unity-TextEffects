using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using TMPEffects;
using UnityEngine;


public class ObservableTagCollection : TagCollection, INotifyCollectionChanged
{
    public ObservableTagCollection(IList<KeyValuePair<EffectTagIndices, EffectTag>> tags, ITMPTagValidator validator = null) : base(tags, validator)
    { }
    public ObservableTagCollection(ITMPTagValidator validator = null) : base(validator)
    { }

    public event NotifyCollectionChangedEventHandler CollectionChanged;


    protected void InvokeEvent(NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
    }

    public override bool TryAdd(EffectTag tag, EffectTagIndices indices)
    {
        if (validator != null && !validator.ValidateTag(tag)) return false;

        int index;
        // If there already is a tag with these exact indices, adjust indices
        if ((index = BinarySearchIndexOf(indices)) > 0)
        {
            AdjustOrderAtIndexAt(index, indices);
        }
        // Otherwise adjust the index
        else index = ~index;

        tags.Insert(index, new KeyValuePair<EffectTagIndices, EffectTag>(indices, tag));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, tags[index]));
        return true;
    }

    public override bool TryAdd(EffectTag tag, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null)
    {
        if (validator != null && !validator.ValidateTag(tag)) return false;

        int index;
        EffectTagIndices indices;

        // If no order is specified, add it as first element at current index
        if (orderAtIndex == null)
        {
            index = BinarySearchIndexOf(new StartIndexOnly(startIndex));

            // If no tag with that startindex
            if (index < 0)
            {
                index = ~index;
                indices = new EffectTagIndices(startIndex, endIndex, 0);
            }
            // Otherwise
            else
            {
                indices = new EffectTagIndices(startIndex, endIndex, tags[index].Key.OrderAtIndex - 1);
            }
        }
        // If order is specified
        else
        {
            index = BinarySearchIndexOf(new TempIndices(startIndex, orderAtIndex.Value));
            indices = new EffectTagIndices(startIndex, endIndex, orderAtIndex.Value);

            // If no tag with these indices
            if (index < 0)
            {
                index = ~index;
            }
            else
            {
                AdjustOrderAtIndexAt(index, indices);
            }
        }

        tags.Insert(index, new KeyValuePair<EffectTagIndices, EffectTag>(indices, tag));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, tags[index]));
        return true;
    }


    public override bool Remove(EffectTag tag)
    {
        int removed = FindIndex(tag);

        if (removed < 0) return false;

        var kvp = tags[removed];
        tags.RemoveAt(removed);
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, kvp));
        return true;
    }

    public override bool RemoveAt(int startIndex, int? order = null)
    {
        int index;
        if (order == null)
        {
            index = BinarySearchIndexOf(new StartIndexOnly(startIndex));
        }
        else
        {
            index = BinarySearchIndexOf(new TempIndices(startIndex, order.Value));
        }

        if (index < 0) return false;

        var kvp = tags[index];
        tags.RemoveAt(index);
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, kvp));
        return true;
    }

    public override int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0)
    {
        List<KeyValuePair<EffectTagIndices, EffectTag>> removed = new List<KeyValuePair<EffectTagIndices, EffectTag>>();
        int first = BinarySearchIndexOf(new StartIndexOnly(startIndex));
        if (first == -1) return 0;

        if (buffer != null)
        {
            int i = 0;
            int len = Mathf.Min(tags.Count, buffer.Length - bufferIndex);
            for (i = first; i < len;)
            {
                KeyValuePair<EffectTagIndices, EffectTag> kvp = tags[i];
                if (kvp.Key.StartIndex != startIndex) break;
                buffer[i] = kvp.Value;
                removed.Add(kvp);
                tags.RemoveAt(i);
            }

            for (; i < tags.Count;)
            {
                KeyValuePair<EffectTagIndices, EffectTag> kvp = tags[i];
                if (kvp.Key.StartIndex != startIndex) break;
                removed.Add(kvp);
                tags.RemoveAt(i);
            }
        }
        else
        {
            for (int i = first; i < tags.Count;)
            {
                KeyValuePair<EffectTagIndices, EffectTag> kvp = tags[i];
                if (kvp.Key.StartIndex != startIndex) break;
                removed.Add(kvp);
                tags.RemoveAt(i);
            }
        }

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed));

        return removed.Count;
    }

    public override void Clear()
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        tags.Clear();
    }


    private class ObservableList : IList<KeyValuePair<EffectTagIndices, EffectTag>>, INotifyCollectionChanged
    {
        private List<KeyValuePair<EffectTagIndices, EffectTag>> list;

        public KeyValuePair<EffectTagIndices, EffectTag> this[int index]
        {
            get => list[index];
            set
            {
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, list[index], value));
                list[index] = value;
            }
        }

        public int Count => list.Count;

        public bool IsReadOnly => ((ICollection<KeyValuePair<EffectTagIndices, EffectTag>>)list).IsReadOnly;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public void Add(KeyValuePair<EffectTagIndices, EffectTag> item)
        {
            list.Add(item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, IndexOf(item)));
        }

        public void Clear()
        {
            list.Clear();
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        public bool Contains(KeyValuePair<EffectTagIndices, EffectTag> item)
        {
            return list.Contains(item);
        }

        public void CopyTo(KeyValuePair<EffectTagIndices, EffectTag>[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<EffectTagIndices, EffectTag>> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public int IndexOf(KeyValuePair<EffectTagIndices, EffectTag> item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, KeyValuePair<EffectTagIndices, EffectTag> item)
        {
            list.Insert(index, item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public bool Remove(KeyValuePair<EffectTagIndices, EffectTag> item)
        {
            if (list.Remove(item))
            {
                CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
                return true;
            }

            return false;
        }

        public void RemoveAt(int index)
        {
            if (CollectionChanged != null)
            {
                var item = list[index];
                list.RemoveAt(index);
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
                return;
            }
            list.RemoveAt(index);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return list.GetEnumerator();
        }
    }
}

public class TagCollection : ITagCollection
{
    protected IList<KeyValuePair<EffectTagIndices, EffectTag>> tags;
    protected readonly ITMPTagValidator validator;

    public TagCollection(IList<KeyValuePair<EffectTagIndices, EffectTag>> tags, ITMPTagValidator validator = null)
    {
        this.validator = validator;
        this.tags = tags;
    }
    public TagCollection(ITMPTagValidator validator = null)
    {
        this.validator = validator;
        this.tags = new List<KeyValuePair<EffectTagIndices, EffectTag>>();
    }

    public virtual bool TryAdd(EffectTag tag, EffectTagIndices indices)
    {
        if (validator != null && !validator.ValidateTag(tag)) return false;

        int index;
        // If there already is a tag with these exact indices, adjust indices
        if ((index = BinarySearchIndexOf(indices)) > 0)
        {
            AdjustOrderAtIndexAt(index, indices);
        }
        // Otherwise adjust the index
        else index = ~index;

        tags.Insert(index, new KeyValuePair<EffectTagIndices, EffectTag>(indices, tag));
        return true;
    }

    public virtual bool TryAdd(EffectTag tag, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null)
    {
        if (validator != null && !validator.ValidateTag(tag)) return false;

        int index;
        EffectTagIndices indices;

        // If no order is specified, add it as first element at current index
        if (orderAtIndex == null)
        {
            index = BinarySearchIndexOf(new StartIndexOnly(startIndex));

            // If no tag with that startindex
            if (index < 0)
            {
                index = ~index;
                indices = new EffectTagIndices(startIndex, endIndex, 0);
            }
            // Otherwise
            else
            {
                indices = new EffectTagIndices(startIndex, endIndex, tags[index].Key.OrderAtIndex - 1);
            }
        }
        // If order is specified
        else
        {
            index = BinarySearchIndexOf(new TempIndices(startIndex, orderAtIndex.Value));
            indices = new EffectTagIndices(startIndex, endIndex, orderAtIndex.Value);

            // If no tag with these indices
            if (index < 0)
            {
                index = ~index;
            }
            else
            {
                AdjustOrderAtIndexAt(index, indices);
            }
        }

        tags.Insert(index, new KeyValuePair<EffectTagIndices, EffectTag>(indices, tag));
        return true;
    }

    protected void AdjustOrderAtIndexAt(int listIndex, EffectTagIndices indices)
    {
        KeyValuePair<EffectTagIndices, EffectTag> current;
        EffectTagIndices last = indices;

        while ((current = tags[listIndex]).Key.StartIndex == last.StartIndex && current.Key.OrderAtIndex == last.OrderAtIndex)
        {
            tags[listIndex++] = new KeyValuePair<EffectTagIndices, EffectTag>(new EffectTagIndices(current.Key.StartIndex, current.Key.EndIndex, current.Key.OrderAtIndex + 1), current.Value);
            last = current.Key;
        }
    }

    public virtual int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0)
    {
        int firstIndex = BinarySearchIndexOf(new StartIndexOnly(startIndex));
        if (firstIndex < 0) return 0;

        int lastIndex = firstIndex;

        do lastIndex++;
        while (lastIndex < tags.Count && tags[lastIndex].Key.StartIndex == startIndex);

        int count = lastIndex - firstIndex;
        if (buffer != null)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferIndex < 0) throw new ArgumentOutOfRangeException(nameof(bufferIndex));

            int len = Mathf.Min(count, buffer.Length - bufferIndex);
            for (int i = 0; i < len; i++)
            {
                buffer[bufferIndex + i] = tags[firstIndex].Value;
                tags.RemoveAt(firstIndex);
            }
        }

        for (int i = 0; i < count; i++)
        {
            tags.RemoveAt(firstIndex);
        }

        return count;
    }

    public virtual bool RemoveAt(int startIndex, int? order = null)
    {
        int index;
        if (order == null)
        {
            index = BinarySearchIndexOf(new StartIndexOnly(startIndex));
        }
        else
        {
            index = BinarySearchIndexOf(new TempIndices(startIndex, order.Value));
        }

        if (index < 0) return false;

        tags.RemoveAt(index);
        return true;
    }

    public virtual void Clear()
    {
        tags.Clear();
    }

    public virtual bool Remove(EffectTag tag)
    {
        int removed = FindIndex(tag);

        if (removed < 0) return false;
        tags.RemoveAt(removed);
        return true;
    }

    public void CopyTo(EffectTag[] array, int arrayIndex)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < tags.Count) throw new ArgumentException(nameof(array));

        for (int i = 0; i < tags.Count; i++)
        {
            array[arrayIndex + i] = tags[i].Value;
        }
    }

    public int TagCount => tags.Count;
    public IEnumerable<EffectTag> Tags
    {
        get
        {
            for (int i = 0; i < tags.Count; i++)
                yield return tags[i].Value;
        }
    }

    public IEnumerable<KeyValuePair<EffectTagIndices, EffectTag>> TagsWithIndices
    {
        get
        {
            for (int i = 0; i < tags.Count; i++)
                yield return tags[i];
        }
    }

    public bool Contains(EffectTag tag, EffectTagIndices? indices = null)
    {
        if (indices == null) return FindIndex(tag) >= 0;
        return FindIndex(tag) >= 0;

        //int index = BinarySearchIndexOf(new TempIndices(tag.StartIndex, tag.OrderAtIndex));
        //if (index < 0) return false;
        //if (tags[index].Value != tag) return false;
        //return true;
    }

    public IEnumerator<EffectTag> GetEnumerator() => Tags.GetEnumerator();

    public EffectTag TagAt(int startIndex, int? order = null)
    {
        int index;
        if (order == null)
        {
            index = BinarySearchIndexOf(new StartIndexOnly(startIndex));
        }
        else
        {
            index = BinarySearchIndexOf(new TempIndices(startIndex, order.Value));
        }

        if (index < 0) return null; // Throw?

        return tags[index].Value;
    }

    public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0)
    {
        int firstIndex = BinarySearchIndexOf(new StartIndexOnly(startIndex));
        if (firstIndex < 0) return 0;

        int lastIndex = firstIndex;

        do lastIndex++;
        while (lastIndex < tags.Count && tags[lastIndex].Key.StartIndex != startIndex);

        int count = lastIndex - firstIndex;
        if (buffer != null)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (bufferIndex < 0) throw new ArgumentOutOfRangeException(nameof(bufferIndex));

            int len = Mathf.Min(count, buffer.Length - bufferIndex);
            for (int i = 0; i < len; i++)
            {
                buffer[bufferIndex + i] = tags[firstIndex].Value;
            }
        }

        return count;
    }

    public IEnumerable<EffectTag> TagsAt(int startIndex)
    {
        int firstIndex = BinarySearchIndexOf(new StartIndexOnly(startIndex));
        if (firstIndex < 0) yield break;

        int lastIndex = firstIndex;

        do yield return tags[lastIndex++].Value;
        while (lastIndex < tags.Count && tags[lastIndex].Key.StartIndex == startIndex);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    protected int FindIndex(EffectTag tag)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (tag == tags[i].Value) return i;
        }

        return -1;
    }
    protected int BinarySearchIndexOf(IComparable<EffectTagIndices> indices)
    {
        int lower = 0;
        int upper = tags.Count - 1;

        while (lower <= upper)
        {
            int middle = lower + (upper - lower) / 2;
            int comparisonResult = indices.CompareTo(tags[middle].Key);
            if (comparisonResult == 0)
                return middle;
            else if (comparisonResult < 0)
                upper = middle - 1;
            else
                lower = middle + 1;
        }

        return ~lower;
    }

    public EffectTagIndices? IndicesOf(EffectTag tag)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i].Value == tag)
                return tags[i].Key;
        }
        return null;
    }

    // TODO Not finished implementing
    public virtual void SetIndicesOf(EffectTag tag, EffectTagIndices newIndices)
    {
        EffectTagIndices? oldIndices = IndicesOf(tag);

        if (oldIndices == null) 
        {
            return; // TODO Throw?
        }

        int index = BinarySearchIndexOf(oldIndices);
        tags.RemoveAt(index);

        index = BinarySearchIndexOf(newIndices);


    }

    public virtual void SetIndicesOf(EffectTag tag, EffectTagIndices oldIndices, EffectTagIndices newIndices)
    {
        throw new NotImplementedException();
    }

    protected struct TempIndices : IComparable<EffectTagIndices>
    {
        private readonly int startIndex;
        private readonly int orderAtIndex;

        public TempIndices(int startIndex, int orderAtIndex)
        {
            this.startIndex = startIndex;
            this.orderAtIndex = orderAtIndex;
        }

        public int CompareTo(EffectTagIndices other)
        {
            int res = startIndex.CompareTo(other.StartIndex);
            if (res == 0) return orderAtIndex.CompareTo(other.OrderAtIndex);
            return res;
        }
    }

    protected struct StartIndexOnly : IComparable<EffectTagIndices>
    {
        private readonly int startIndex;

        public StartIndexOnly(int startIndex)
        {
            this.startIndex = startIndex;
        }

        public int CompareTo(EffectTagIndices other)
        {
            return startIndex.CompareTo(other.StartIndex);
        }
    }
}

public class ReadOnlyTagCollection : IReadOnlyTagCollection
{
    private IReadOnlyTagCollection collection;

    internal ReadOnlyTagCollection(List<KeyValuePair<EffectTagIndices, EffectTag>> tags)
    {
        this.collection = new TagCollection(tags);
    }

    internal ReadOnlyTagCollection(IReadOnlyTagCollection collection)
    {
        this.collection = collection;
    }

    public int TagCount => collection.TagCount;

    public IEnumerable<EffectTag> Tags => collection.Tags;

    public IEnumerable<KeyValuePair<EffectTagIndices, EffectTag>> TagsWithIndices => collection.TagsWithIndices;

    //public bool Contains(EffectTag tag)
    //{
    //    return collection.Contains(tag);
    //}

    public bool Contains(EffectTag tag, EffectTagIndices? indices = null)
    {
        return collection.Contains(tag, indices);
    }

    public IEnumerator<EffectTag> GetEnumerator()
    {
        return collection.GetEnumerator();
    }

    public EffectTagIndices? IndicesOf(EffectTag tag)
    {
        return collection.IndicesOf(tag);
    }

    public EffectTag TagAt(int startIndex, int? order = null)
    {
        return collection.TagAt(startIndex, order);
    }

    public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0)
    {
        return collection.TagsAt(startIndex, buffer, bufferIndex);
    }

    public IEnumerable<EffectTag> TagsAt(int startIndex)
    {
        return collection.TagsAt(startIndex);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return collection.GetEnumerator();
    }
}


/// <summary>
/// A writable collection of tags.
/// </summary>
public interface ITagCollection : IReadOnlyTagCollection
{
    public bool TryAdd(EffectTag tag, EffectTagIndices indices);
    public bool TryAdd(EffectTag tag, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null);

    public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0);
    public bool RemoveAt(int startIndex, int? order = null);

    public bool Remove(EffectTag tagData);

    public void Clear();

    public void SetIndicesOf(EffectTag tag, EffectTagIndices newIndices);
    public void SetIndicesOf(EffectTag tag, EffectTagIndices oldIndices, EffectTagIndices newIndices);
    // TODO 
    // Maybe allow setting the indices of an already added collection
    //public bool SetIndices(EffectTag tag, EffectTagIndices indices);
}

/// <summary>
/// A readonly collection of tags.
/// </summary>
public interface IReadOnlyTagCollection : IReadOnlyCollection<EffectTag>
{
    public int TagCount { get; }
    public IEnumerable<EffectTag> Tags { get; }
    public IEnumerable<KeyValuePair<EffectTagIndices, EffectTag>> TagsWithIndices { get; }

    public bool Contains(EffectTag tag, EffectTagIndices? indices = null);

    public EffectTagIndices? IndicesOf(EffectTag tag);

    public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0);
    public IEnumerable<EffectTag> TagsAt(int startIndex);
    public EffectTag TagAt(int startIndex, int? order = null);

    int IReadOnlyCollection<EffectTag>.Count => TagCount;
}
