using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;
using TMPEffects;
using System.Linq;

public interface ITagManager<TKey> : IReadOnlyDictionary<TKey, ObservableTagCollection>
{
    public ObservableTagCollection AddKey(TKey key);
    public bool RemoveKey(TKey key);
}

public class TagManager<TKey> : ITagCollection, ITagManager<TKey>, IReadOnlyDictionary<TKey, ObservableTagCollection>, INotifyCollectionChanged where TKey : ITMPPrefixSupplier, ITMPTagValidator
{
    private readonly NonAdjustingTagCollection union;
    private readonly Dictionary<TKey, ObservableTagCollection> collections;
    private readonly Dictionary<char, TKey> prefixToKey;

    private bool autoSync;

    public event NotifyCollectionChangedEventHandler CollectionChanged
    {
        add => union.CollectionChanged += value;
        remove => union.CollectionChanged -= value;
    }

    public TagManager()
    {
        union = new NonAdjustingTagCollection();
        collections = new Dictionary<TKey, ObservableTagCollection>();
        prefixToKey = new Dictionary<char, TKey>();
        autoSync = false;
    }

    public ObservableTagCollection AddKey(TKey key)
    {
        ObservableTagCollection collection = new ObservableTagCollection();

        collection.CollectionChanged += OnCollectionChanged;
        prefixToKey.Add(key.Prefix, key);
        collections.Add(key, collection);

        return collection;
    }

    public bool RemoveKey(TKey key)
    {
        if (!collections.ContainsKey(key)) return false;

        collections[key].CollectionChanged -= OnCollectionChanged;
        collections.Remove(key);
        prefixToKey.Remove(key.Prefix);

        return true;
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
    {
        if (autoSync) return;

        KeyValuePair<EffectTagIndices, EffectTag> kvp;
        switch (args.Action)
        {
            case NotifyCollectionChangedAction.Add:
                kvp = (KeyValuePair<EffectTagIndices, EffectTag>)args.NewItems[0];
                if (!union.TryAdd(kvp.Value, kvp.Key))
                {
                    Debug.LogError("Failed to add to union; now undefined");
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (var current in args.OldItems)
                {
                    kvp = (KeyValuePair<EffectTagIndices, EffectTag>)current;
                    if (!union.RemoveAt(kvp.Key.StartIndex, kvp.Key.OrderAtIndex))
                    {
                        Debug.LogError("Failed to remove from union; now undefined");
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                List<KeyValuePair<EffectTagIndices, EffectTag>> list;
                IEnumerable<KeyValuePair<EffectTagIndices, EffectTag>> concat = new List<KeyValuePair<EffectTagIndices, EffectTag>>();

                foreach (var coll in collections.Values)
                {
                    concat.Concat(coll.TagsWithIndices);
                }

                concat = concat.OrderBy(x => x.Key).ToList();
                break;

            case NotifyCollectionChangedAction.Move:
                throw new System.NotImplementedException();

            case NotifyCollectionChangedAction.Replace:
                throw new System.NotImplementedException();
        }
    }

    #region IReadOnlyDictionary
    public IEnumerable<TKey> Keys => collections.Keys;

    public IEnumerable<ObservableTagCollection> Values => collections.Values;

    public int KeyCount => collections.Count;
    int IReadOnlyCollection<KeyValuePair<TKey, ObservableTagCollection>>.Count => collections.Count;

    public ObservableTagCollection this[TKey key] => collections[key];

    public bool ContainsKey(TKey key)
    {
        return collections.ContainsKey(key);
    }

    public bool TryGetValue(TKey key, out ObservableTagCollection value)
    {
        return collections.TryGetValue(key, out value);
    }

    IEnumerator<KeyValuePair<TKey, ObservableTagCollection>> IEnumerable<KeyValuePair<TKey, ObservableTagCollection>>.GetEnumerator()
    {
        return collections.GetEnumerator();
    }
    #endregion

    #region ITagCollection
    public bool TryAdd(EffectTag tag, EffectTagIndices indices)
    {
        return TryAdd(tag, indices.StartIndex, indices.EndIndex, indices.OrderAtIndex);
    }

    public bool TryAdd(EffectTag tag, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null)
    {
        try
        {
            autoSync = true;

            if (!prefixToKey.TryGetValue(tag.Prefix, out TKey key))
                return false;

            if (!collections[key].TryAdd(tag, startIndex, endIndex, orderAtIndex))
                return false;

            if (!union.TryAdd(tag, startIndex, endIndex, orderAtIndex))
            {
                Debug.LogError("Added to collection but failed to add to union; now undefined");
                return false;
            }

            ValidateIndices();
            return true;
        }
        finally
        {
            autoSync = false;
        }
    }

    private void ValidateIndices()
    {
        List<KeyValuePair<EffectTagIndices, EffectTag>> list = union.TagsWithIndices.ToList();

        if (list.Count == 0) return;

        autoSync = true;

        try
        {
            EffectTagIndices last = list[0].Key;
            for (int i = 1; i < list.Count; i++)
            {
                var current = list[i];
                int comparison = last.CompareTo(current.Key);

                // If equal, adjust
                if (comparison == 0)
                {
                    EffectTagIndices newIndices = new EffectTagIndices(current.Key.StartIndex, current.Key.EndIndex, current.Key.OrderAtIndex + 1);

                    union.SetIndicesOf(current.Value, current.Key, newIndices);
                    collections[prefixToKey[current.Value.Prefix]].SetIndicesOf(current.Value, current.Key, newIndices);
                }

                // If last larger than current, adjust
                else if (comparison > 0)
                {
                    Debug.LogWarning("The last indices were larger than the current one; Idk if that case is valid or not");
                    EffectTagIndices lastIndices = list[i - 1].Key;
                    EffectTagIndices newIndices = new EffectTagIndices(lastIndices.StartIndex, lastIndices.EndIndex, lastIndices.OrderAtIndex + 1);

                    union.SetIndicesOf(current.Value, current.Key, newIndices);
                    collections[prefixToKey[current.Value.Prefix]].SetIndicesOf(current.Value, current.Key, newIndices);
                }

                // else we good
                last = current.Key;
            }
        }
        catch
        {
            Debug.LogError("Failed to validate indices in tagmanager; shouldnt be possible");
            throw;
        }
        finally
        {
            autoSync = false;
        }
    }

    public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0)
    {
        try
        {
            autoSync = true;
            foreach (var collection in collections.Values)
            {
                collection.RemoveAllAt(startIndex);
            }

            return union.RemoveAllAt(startIndex, buffer, bufferIndex);
        }
        finally
        {
            autoSync = false;
        }
    }

    public bool RemoveAt(int startIndex, int? order = null)
    {
        var tag = union.TagAt(startIndex, order);
        return Remove(tag);
    }

    public bool Remove(EffectTag tag)
    {
        if (!prefixToKey.TryGetValue(tag.Prefix, out TKey key)) return false;

        try
        {
            bool success = collections[key].Remove(tag);
            autoSync = false;

            if (success)
            {
                if (!union.Remove(tag))
                {
                    Debug.LogError("Failed to remove from union but did remove from subcollection; now undefined");
                }

                return true;
            }

            return false;
        }
        finally
        {
            autoSync = false;
        }
    }

    public void Clear()
    {
        try
        {
            autoSync = true;

            union.Clear();
            foreach (var collection in collections.Values)
                collection.Clear();
        }
        finally
        {
            autoSync = false;
        }
    }

    public int TagCount => union.TagCount;
    public IEnumerable<EffectTag> Tags => union.Tags;
    public IEnumerable<KeyValuePair<EffectTagIndices, EffectTag>> TagsWithIndices => union.TagsWithIndices;
    public bool Contains(EffectTag tag, EffectTagIndices? indices = null) => union.Contains(tag, indices);
    public EffectTagIndices? IndicesOf(EffectTag tag) => union.IndicesOf(tag);
    public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0) => union.TagsAt(startIndex, buffer, bufferIndex);
    public IEnumerable<EffectTag> TagsAt(int startIndex) => union.TagsAt(startIndex);
    public EffectTag TagAt(int startIndex, int? order = null) => union.TagAt(startIndex, order);
    public IEnumerator<EffectTag> GetEnumerator() => union.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => union.GetEnumerator();
    public void SetIndicesOf(EffectTag tag, EffectTagIndices newIndices)
    {
        try
        {
            autoSync = true;
            union.SetIndicesOf(tag, newIndices);
            ValidateIndices();
        }
        finally
        {
            autoSync = false;
        }
    }
    public void SetIndicesOf(EffectTag tag, EffectTagIndices oldIndices, EffectTagIndices newIndices)
    {
        try
        {
            autoSync = true;
            union.SetIndicesOf(tag, oldIndices, newIndices);
            ValidateIndices();
        }
        finally
        {
            autoSync = false;
        }
    }
    #endregion

    // TODO
    // Right now, (Im pretty sure) other listeners (specifically CachedCollection) rely on 
    // indices only being changed via removing and reinsertion
    // => Implement NotifyCollectionChanged more robustly and raise replace / move events
    private class NonAdjustingTagCollection : ObservableTagCollection
    {
        public override bool TryAdd(EffectTag tag, EffectTagIndices indices)
        {
            if (validator != null && !validator.ValidateTag(tag)) return false;

            int index;
            if ((index = BinarySearchIndexOf(indices)) < 0)
                index = ~index;

            tags.Insert(index, new KeyValuePair<EffectTagIndices, EffectTag>(indices, tag));
            InvokeEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, tags[index]));
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
            }

            tags.Insert(index, new KeyValuePair<EffectTagIndices, EffectTag>(indices, tag));
            InvokeEvent(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, tags[index]));
            return true;
        }

        public override void SetIndicesOf(EffectTag tag, EffectTagIndices oldIndices, EffectTagIndices newIndices)
        {
            int index = BinarySearchIndexOf(oldIndices);

            if (index < 0)
            {
                return; // TODO Throw?
            }

            tags.RemoveAt(index);

            index = BinarySearchIndexOf(newIndices);

            if (index < 0)
                index = ~index;

            tags.Insert(index, new KeyValuePair<EffectTagIndices, EffectTag>(newIndices, tag));
        }
    }
}