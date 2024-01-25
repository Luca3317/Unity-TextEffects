using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TMPEffects.Tags
{
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
}