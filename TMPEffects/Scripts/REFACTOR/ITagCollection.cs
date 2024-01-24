using System.Collections.Generic;
using System;
using TMPEffects.Tags;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using TMPEffects;
using System.Collections.ObjectModel;
using System.Collections.Specialized;


public class NotifyProcessorsChangedEventArgs : EventArgs
{
    public NotifyProcessorsChangedAction Action { get; }

    public char Key { get; }
    public int Index { get; }

    public NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction action)
    {
        Action = action;
    }
    public NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction action, char key)
    {
        Action = action;
        Key = key;
    }
    public NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction action, char key, int index)
    {
        Action = action;
        Key = key;
        Index = index;
    }


}

public enum NotifyProcessorsChangedAction : short
{
    Added = 0,
    Removed = 10
}


public delegate void NotifyProcessorsChangedEventHandler(object sender, NotifyProcessorsChangedEventArgs e);

public interface INotifyProcessorsChanged
{

    event NotifyProcessorsChangedEventHandler ProcessorsChanged;
}


internal interface ITagProcessorManager : IEnumerable<TagProcessor>, INotifyProcessorsChanged
{
    public ReadOnlyDictionary<char, ReadOnlyCollection<TagProcessor>> TagProcessors { get; }

    public void RegisterProcessor(char prefix, TagProcessor processor, int priority = 0);
    public bool UnregisterProcessor(char prefix, TagProcessor processor);
}


internal class TagProcessorManager : ITagProcessorManager
{
    public ReadOnlyDictionary<char, ReadOnlyCollection<TagProcessor>> TagProcessors { get; private set; }

    public event NotifyProcessorsChangedEventHandler ProcessorsChanged;

    private Dictionary<char, List<TagProcessor>> tagProcessors;
    private Dictionary<char, ReadOnlyCollection<TagProcessor>> tagProcessorsRO;

    public TagProcessorManager()
    {
        tagProcessors = new();
        tagProcessorsRO = new();
        TagProcessors = new(tagProcessorsRO);
    }

    public void RegisterProcessor(char prefix, TagProcessor processor, int priority = 0)
    {
        if (processor == null) throw new System.ArgumentNullException(nameof(processor));

        List<TagProcessor> processors;
        if (tagProcessors.TryGetValue(prefix, out processors))
        {
            if (priority > processors.Count || priority < 0)
            {
                processors.Add(processor);
                ProcessorsChanged(this, new NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction.Added, prefix, processors.Count));
            }
            else
            {
                processors.Insert(priority, processor);
                ProcessorsChanged(this, new NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction.Added, prefix, priority));
            }
        }
        else
        {
            processors = new List<TagProcessor>() { processor };
            tagProcessors.Add(prefix, processors);
            tagProcessorsRO.Add(prefix, new ReadOnlyCollection<TagProcessor>(processors));
            ProcessorsChanged(this, new NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction.Added, prefix, 0));
        }
    }

    public bool UnregisterProcessor(char prefix, TagProcessor processor)
    {
        if (processor == null) throw new System.ArgumentNullException(nameof(processor));

        List<TagProcessor> processors;
        if (!tagProcessors.TryGetValue(prefix, out processors)) return false;

        int index = processors.IndexOf(processor);
        if (!processors.Remove(processor)) return false;

        ProcessorsChanged(this, new NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction.Removed, prefix, index));

        if (processors.Count == 0)
        {
            tagProcessors.Remove(prefix);
            tagProcessorsRO.Remove(prefix);
        }

        return true;
    }

    public IEnumerator<TagProcessor> GetEnumerator()
    {
        foreach (var list in tagProcessors.Values)
            foreach (var processor in list)
                yield return processor;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}










public interface IReadOnlyCachedTagManager<TKey, TCached> : IReadOnlyTagManager<TKey>, IReadOnlyCachedTagCollection<TCached> where TCached : ITagWrapper
{
    public IReadOnlyCachedTagCollection<TCached> CachedFor(TKey key);
}

public interface ICachedTagManager<TKey, TCached> : IReadOnlyCachedTagManager<TKey, TCached>, ICachedTagCollection<TCached> where TCached : ITagWrapper
{

}

public interface IReadOnlyTagManager<TKey> : IReadOnlyTagCollection
{
    public IReadOnlyTagCollection TagsFor(TKey key);
    public IEnumerable<TKey> Keys { get; }
    public int KeyCount { get; }
    public bool ContainsKey(TKey key);
}

public interface ITagManager<TKey> : IReadOnlyTagManager<TKey>, ITagCollection
{
    public void AddKey(TKey key);
    public void RemoveKey(TKey key);
}



public class TagManager<TKey, TCached> : ICachedTagManager<TKey, TCached> where TKey : TMPEffectCategory where TCached : ITagWrapper
{
    private TagCollection<TCached> allCached;

    private Dictionary<TKey, TagCollection<TCached>> collections;
    private Dictionary<TKey, ReadOnlyTagCollection<TCached>> collectionsRO;
    private Dictionary<TKey, ReadOnlyTagCollection> tagsRO;
    private Dictionary<char, TKey> prefixToKey;

    private ITagCacher<TCached> tagCacher;

    public void AddKey(TKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        if (collections.ContainsKey(key)) throw new ArgumentException(nameof(key));

        var collection = new TagCollection<TCached>(tagCacher);
        collections[key] = collection;
        collectionsRO[key] = new ReadOnlyTagCollection<TCached>(collection);
        tagsRO[key] = new ReadOnlyTagCollection(collection);
    }
    public bool RemoveKey(TKey key)
    {
        TagCollection<TCached> collection;
        if (!collections.TryGetValue(key, out collection)) return false;

        foreach (var tag in collection)
        {
            allCached.Remove(tag);
        }

        collections.Remove(key);
        collectionsRO.Remove(key);
        tagsRO.Remove(key);

        return true;
    }

    public IReadOnlyTagCollection TagsFor(TKey key) => tagsRO[key];
    public IReadOnlyCachedTagCollection<TCached> CachedFor(TKey key) => collectionsRO[key];
    public bool ContainsKey(TKey key) => collections.ContainsKey(key);
    public IEnumerable<TKey> Keys => collections.Keys;
    public int KeyCount => collections.Keys.Count;

    #region ITagCacher Implementation
    public IEnumerable<TCached> GetCached() => allCached.GetCached();
    public IEnumerable<TCached> GetCached(int index) => allCached.GetCached(index);
    public TCached GetCached(int index, int? order = null) => allCached.GetCached(index, order);
    public TCached GetCached(EffectTag tag) => allCached.GetCached(tag);
    #endregion

    #region ITagCollection Implementation
    public int TagCount => allCached.TagCount;
    public IEnumerable<EffectTag> Tags => allCached.Tags;

    public bool TryAdd(EffectTag tag)
    {
        TKey key;
        if (!prefixToKey.TryGetValue(tag.Prefix, out key)) return false;

        if (!collections[key].TryAdd(tag)) return false;

        if (allCached.TryAdd(tag)) return true;

        // If failed to add to allcached but added to sub cacher
        // This likely means there already was a tag with the same 
        // startIndex and order in another subcacher
        // (only valid reason I can think of)
        // TODO should i maybe allow that?

        if (!collections[key].Remove(tag))
        {
            Debug.LogError("Tag was added to sub collection but not super collection; The TagManager is now undefined.\nThis is a state that shouldnt be possible to achieve - bug");
        }

        return false;
    }

    public void Clear()
    {
        foreach (var cacher in collections.Values) cacher.Clear();
        allCached.Clear();
    }

    public bool Remove(EffectTag tag)
    {
        TKey key;
        if (!prefixToKey.TryGetValue(tag.Prefix, out key)) return false;

        if (!collections[key].Remove(tag)) return false;

        if (allCached.Remove(tag)) return true;

        Debug.LogError("Tag was removed from sub collection but not super collection; The TagManager is now undefined.\nThis is a state that shouldnt be possible to achieve - bug");

        return false;
    }

    public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0)
    {
        int count = 0;
        foreach (var cacher in collections.Values)
        {
            count += cacher.RemoveAllAt(startIndex);
        }
        if (count != allCached.RemoveAllAt(startIndex, buffer, bufferIndex))
        {
            Debug.LogError("Removed inconsistent amount of tags between sub and super collection; The TagManager is now undefined.\nThis is a state that shouldnt be possible to achieve - bug");
        }
        return count;
    }

    public bool RemoveAt(int startIndex, int? order = null)
    {
        EffectTag tag = allCached.TagAt(startIndex, order);

        if (tag == null) return false;

        TKey key;
        if (!prefixToKey.TryGetValue(tag.Prefix, out key))
        {
            Debug.LogError("During removal, it appears there was a tag that is included in the super collection but does not have a valid prefix; The TagManager is in undefined state.\nThis is a state that shouldnt be possible to achieve - bug");
            return false;
        }

        if (!allCached.Remove(tag))
        {
            Debug.LogError("Failed to remove tag from super collection - bug");
            return false;
        }

        if (!collections[key].Remove(tag))
        {
            Debug.LogError("Failed to remove tag from sub collection - bug");
            return false;
        }

        return true;
    }

    public bool Contains(EffectTag tag)
    {
        TKey key;
        if (!prefixToKey.TryGetValue(tag.Prefix, out key)) return false;

        return collections[key].Contains(tag);
    }

    public void CopyTo(EffectTag[] array, int arrayIndex)
    {
        if (array == null) throw new System.ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < allCached.TagCount) throw new ArgumentException(nameof(array));

        int i = 0;
        foreach (var cached in allCached)
        {
            array[i++] = cached;
        }
    }

    public IEnumerator<EffectTag> GetEnumerator() => allCached.GetEnumerator();
    public EffectTag TagAt(int startIndex, int? order = null) => allCached.TagAt(startIndex, order);
    public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0) => allCached.TagsAt(startIndex, buffer, bufferIndex);
    public IEnumerable<EffectTag> TagsAt(int startIndex) => allCached.TagsAt(startIndex);
    IEnumerator IEnumerable.GetEnumerator() => allCached.GetEnumerator();
    #endregion
}


public interface ITagWrapper
{
    public EffectTag Tag { get; }
}

public interface ITagCacher<T> where T : ITagWrapper
{
    public bool TryCache(EffectTag tag, out T cached);
}

public interface IReadOnlyCachedTagCollection<T> : IReadOnlyTagCollection where T : ITagWrapper
{
    public IEnumerable<T> GetCached();
    public IEnumerable<T> GetCached(int index);
    public T GetCached(int index, int? order = null);
    public T GetCached(EffectTag tag);
}

public interface ICachedTagCollection<T> : ITagCollection, IReadOnlyCachedTagCollection<T> where T : ITagWrapper
{

}


public class TagCollection<T> : TagCollectionBase<T>, ICachedTagCollection<T> where T : ITagWrapper
{
    private ITagCacher<T> cacher;

    public TagCollection(ITagCacher<T> cacher)
    {
        this.cacher = cacher;
    }

    public override bool TryAdd(EffectTag tag)
    {
        int i = 0;
        for (; i < tags.Count; i++)
        {
            if (tags[i].Tag.StartIndex < tag.StartIndex) continue;
            if (tags[i].Tag.StartIndex == tag.StartIndex)
            {
                if (tags[i].Tag.OrderAtIndex == tag.OrderAtIndex)
                {
                    return false;
                }
                else if (tags[i].Tag.OrderAtIndex > tag.OrderAtIndex)
                {
                    break;
                }
            }
            else break;
        }

        T t;
        if (!cacher.TryCache(tag, out t))
            return false;

        tags.Insert(i, t);
        return true;
    }

    protected override EffectTag Tag(int i) => tags[i].Tag;

    public IEnumerable<T> GetCached()
    {
        foreach (var tag in tags) yield return tag;
    }

    public IEnumerable<T> GetCached(int index)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i].Tag.StartIndex < index) continue;
            if (tags[i].Tag.StartIndex == index)
            {
                yield return tags[i];
            }
            else break;
        }
    }

    public T GetCached(int index, int? order = null)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i].Tag.StartIndex < index) continue;
            if (tags[i].Tag.StartIndex == index)
            {
                if (order == null || tags[i].Tag.OrderAtIndex == order)
                    return tags[i];
            }
            else break;
        }

        return default;
    }

    public T GetCached(EffectTag tag)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (tags[i].Tag.StartIndex < tag.StartIndex) continue;
            if (tags[i].Tag.StartIndex == tag.StartIndex)
            {
                if (tags[i].Tag == tag)
                    return tags[i];
            }
            else break;
        }

        return default;
    }
}

public class TagCollection : TagCollectionBase<EffectTag>
{
    public override bool TryAdd(EffectTag tag)
    {
        throw new NotImplementedException();
    }

    // TODO Does unity c# compiler support devirtualization?
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override EffectTag Tag(int i) => tags[i];
}

public abstract class TagCollectionBase<T> : ITagCollection
{
    protected List<T> tags;

    // TODO Does unity c# compiler support devirtualization?
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected abstract EffectTag Tag(int i);

    public abstract bool TryAdd(EffectTag tag);

    public int TagCount => tags.Count;
    public IEnumerable<EffectTag> Tags
    {
        get
        {
            for (int i = 0; i < tags.Count; i++)
                yield return Tag(i);
        }
    }

    public void Clear()
    {
        tags.Clear();
    }

    public bool Contains(EffectTag item)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex < item.StartIndex) continue;
            if (Tag(i).StartIndex == item.StartIndex)
            {
                if (Tag(i) == item) return true;
            }
            else break;
        }

        return false;
    }

    public void CopyTo(EffectTag[] array, int arrayIndex)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < tags.Count) throw new ArgumentException(nameof(array));

        for (int i = 0; i < tags.Count; i++)
        {
            array[arrayIndex + i] = Tag(i);
        }
    }

    public IEnumerator<EffectTag> GetEnumerator() => Tags.GetEnumerator();

    public bool Remove(EffectTag item)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex < item.StartIndex) continue;
            if (Tag(i).StartIndex == item.StartIndex)
            {
                if (Tag(i) == item)
                {
                    tags.RemoveAt(i);
                    return true;
                }
            }
            else break;
        }

        return false;
    }

    public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0)
    {
        int i = 0;
        for (; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex < startIndex) continue;
            if (Tag(i).StartIndex == startIndex)
            {
                break;
            }
            else return 0;
        }

        int index = i;
        int count = 0;
        for (; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex != startIndex) break;
            count++;
        }

        if (count == 0) return 0;

        if (buffer != null)
        {
            i = 0;
            int len = Mathf.Min(count, buffer.Length - bufferIndex);
            for (; i < len; i++)
            {
                buffer[bufferIndex + i] = Tag(i + index);
            }
        }

        tags.RemoveRange(index, count);
        return count;
    }

    public bool RemoveAt(int startIndex, int? order = null)
    {
        int i = 0;
        for (; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex < startIndex) continue;
            if (Tag(i).StartIndex == startIndex)
            {
                if (order == null || Tag(i).OrderAtIndex == order)
                {
                    tags.RemoveAt(i);
                    return true;
                }
            }
            else break;
        }

        return false;
    }

    public EffectTag TagAt(int startIndex, int? order = null)
    {
        int i = 0;
        for (; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex < startIndex) continue;
            if (Tag(i).StartIndex == startIndex)
            {
                if (order == null || Tag(i).OrderAtIndex == order)
                {
                    return Tag(i);
                }
            }
            else break;
        }

        return null;
    }

    public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0)
    {
        int i = 0;
        for (; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex < startIndex) continue;
            if (Tag(i).StartIndex == startIndex)
            {
                break;
            }
            else return 0;
        }

        int index = i;
        int count = 0;
        for (; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex != startIndex) break;
            count++;
        }

        if (count == 0) return 0;

        i = 0;
        int len = Mathf.Min(count, buffer.Length - bufferIndex);
        for (; i < len; i++)
        {
            buffer[bufferIndex + i] = Tag(i + index);
        }

        return count;
    }

    public IEnumerable<EffectTag> TagsAt(int startIndex)
    {
        int i = 0;
        for (; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex < startIndex) continue;
            if (Tag(i).StartIndex == startIndex)
            {
                yield return Tag(i);
            }
            else break;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }


    private int GetIndex(int startIndex)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (Tag(i).StartIndex < startIndex) continue;
            if (Tag(i).StartIndex == startIndex) return i;
            else break;
        }

        return -1;
    }
}

public class ReadOnlyTagCollection<T> : ReadOnlyTagCollection, IReadOnlyCachedTagCollection<T> where T : ITagWrapper
{
    private ICachedTagCollection<T> collection;

    public ReadOnlyTagCollection(ICachedTagCollection<T> collection) : base(collection)
    {
        this.collection = collection;
    }

    public IEnumerable<T> GetCached() => collection.GetCached();
    public IEnumerable<T> GetCached(int index) => collection.GetCached(index);
    public T GetCached(int index, int? order = null) => collection.GetCached(index, order);
    public T GetCached(EffectTag tag) => collection.GetCached(tag);
}

public class ReadOnlyTagCollection : IReadOnlyTagCollection
{
    private readonly IReadOnlyTagCollection collection;

    public ReadOnlyTagCollection(IReadOnlyTagCollection collection)
    {
        this.collection = collection;
    }

    public int TagCount => collection.TagCount;

    public IEnumerable<EffectTag> Tags => collection.Tags;

    public bool Contains(EffectTag tag)
    {
        return collection.Contains(tag);
    }

    public IEnumerator<EffectTag> GetEnumerator()
    {
        return collection.GetEnumerator();
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
        return ((IEnumerable)collection).GetEnumerator();
    }
}

public interface ITagCollection : ICollection<EffectTag>, IReadOnlyTagCollection
{
    public bool TryAdd(EffectTag tag);

    public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0);
    public bool RemoveAt(int startIndex, int? order = null);


    int ICollection<EffectTag>.Count => TagCount;
    void ICollection<EffectTag>.Add(EffectTag item)
    {
        if (!TryAdd(item)) throw new ArgumentException(nameof(item));
    }
    bool ICollection<EffectTag>.IsReadOnly => false;
}

public interface IReadOnlyTagCollection : IReadOnlyCollection<EffectTag>
{
    public int TagCount { get; }
    public IEnumerable<EffectTag> Tags { get; }

    public bool Contains(EffectTag tag);

    public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0);
    public IEnumerable<EffectTag> TagsAt(int startIndex);
    public EffectTag TagAt(int startIndex, int? order = null);

    int IReadOnlyCollection<EffectTag>.Count => TagCount;
}