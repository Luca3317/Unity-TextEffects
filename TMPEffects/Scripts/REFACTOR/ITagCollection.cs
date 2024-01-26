using System.Collections.Generic;
using System;
using TMPEffects.Tags;
using System.Collections;
using System.Runtime.CompilerServices;
using UnityEngine;
using TMPEffects;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;
using UnityEngine.UIElements;
using Unity.Plastic.Newtonsoft.Json.Linq;
using Codice.CM.Common;
using System.ComponentModel;
using UnityEditor.ShaderKeywordFilter;

namespace TMPEffects.Tags
{
    public class ObservableTagCollection
    {
        private ObservableList<EffectTag> list;

        internal ObservableTagCollection(ObservableList<EffectTag> list)
        {
            this.list = list;
        }
        public ObservableTagCollection()
        {
            this.list = new ObservableList<EffectTag>();
        }
    }


    public class ObservableReadOnlyTagCollection
    {
        public ObservableReadOnlyTagCollection(ObservableTagCollection tagCollection) 
        {

        }
    }





    /// <summary>
    /// A writable collection of <see cref="ITagWrapper"/>.
    /// </summary>
    /// <typeparam name="T">The type of the TagWrapper</typeparam>
    public class TagCollection<T> : ITagCollection, ITagCollection<T> where T : ITagWrapper
    {
        private ITagCollection<T> collection;

        public int TagCount => collection.TagCount;
        public IEnumerable<EffectTag> Tags => collection.Tags;

        public TagCollection(ITagCollection<T> collection)
        {
            this.collection = collection;
        }
        public TagCollection(ITagCacher<T> cacher, IList<T> list)
        {
            collection = new TagCollectionIMPL<T>(cacher, list);
        }
        public TagCollection(ITagCacher<T> cacher) : base()
        {
            collection = new TagCollectionIMPL<T>(cacher);
        }

        public IEnumerable<T> GetCached() => collection.GetCached();
        public IEnumerable<T> GetCached(int index) => collection.GetCached(index);
        public T GetCached(int index, int? order = null) => collection.GetCached(index, order);
        public T GetCached(EffectTag tag) => collection.GetCached(tag);

        public bool TryAdd(EffectTag tag) => collection.TryAdd(tag);
        public bool TryAdd(EffectTagData data, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null) => collection.TryAdd(data, startIndex, endIndex, orderAtIndex);
        public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0) => collection.RemoveAllAt(startIndex, buffer, bufferIndex);
        public bool RemoveAt(int startIndex, int? order = null) => collection.RemoveAt(startIndex, order);
        public void Clear() => collection.Clear();
        public bool Contains(EffectTag item) => collection.Contains(item);
        public void CopyTo(EffectTag[] array, int arrayIndex) => collection.CopyTo(array, arrayIndex);
        public bool Remove(EffectTag item) => collection.Remove(item);
        public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0) => collection.TagsAt(startIndex, buffer, bufferIndex);
        public IEnumerable<EffectTag> TagsAt(int startIndex) => collection.TagsAt(startIndex);
        public EffectTag TagAt(int startIndex, int? order = null) => collection.TagAt(startIndex, order);
        public IEnumerator<EffectTag> GetEnumerator() => collection.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => collection.GetEnumerator();
    }

    /// <summary>
    /// A writable collection of tags.
    /// </summary>
    public class TagCollection : ITagCollection
    {
        public int TagCount => collection.TagCount;
        public IEnumerable<EffectTag> Tags => collection.Tags;

        private ITagCollection collection;

        public TagCollection(ITagCollection collection)
        {
            this.collection = collection;
        }
        public TagCollection(IList<EffectTag> list)
        {
            collection = new TagCollectionIMPL(list);
        }
        public TagCollection() : base()
        {
            collection = new TagCollectionIMPL();
        }

        public bool TryAdd(EffectTag tag) => collection.TryAdd(tag);
        public bool TryAdd(EffectTagData data, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null) => collection.TryAdd(data, startIndex, endIndex, orderAtIndex);
        public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0) => collection.RemoveAllAt(startIndex, buffer, bufferIndex);
        public bool RemoveAt(int startIndex, int? order = null) => collection.RemoveAt(startIndex, order);
        public void Clear() => collection.Clear();
        public bool Contains(EffectTag item) => collection.Contains(item);
        public void CopyTo(EffectTag[] array, int arrayIndex) => collection.CopyTo(array, arrayIndex);
        public bool Remove(EffectTag item) => collection.Remove(item);
        public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0) => collection.TagsAt(startIndex, buffer, bufferIndex);
        public IEnumerable<EffectTag> TagsAt(int startIndex) => collection.TagsAt(startIndex);
        public EffectTag TagAt(int startIndex, int? order = null) => collection.TagAt(startIndex, order);
        public IEnumerator<EffectTag> GetEnumerator() => collection.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => collection.GetEnumerator();
    }

    internal class TagCollectionIMPL<T> : TagCollectionIMPL_Base<T>, ITagCollection<T> where T : ITagWrapper
    {
        private ITagCacher<T> cacher;

        public TagCollectionIMPL(ITagCacher<T> cacher) : base()
        {
            this.cacher = cacher;
        }

        public TagCollectionIMPL(ITagCacher<T> cacher, IList<T> tags) : base(tags)
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

        public override bool TryAdd(EffectTagData data, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null)
        {
            throw new NotImplementedException();
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

    internal class TagCollectionIMPL : TagCollectionIMPL_Base<EffectTag>
    {
        public TagCollectionIMPL(IList<EffectTag> list) : base(list)
        { }
        public TagCollectionIMPL() : base()
        { }

        public override bool TryAdd(EffectTag tag)
        {
            throw new NotImplementedException();
        }

        public override bool TryAdd(EffectTagData data, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null)
        {
            throw new NotImplementedException();
        }

        // TODO Does unity c# compiler support devirtualization?
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected override EffectTag Tag(int i) => tags[i];
    }

    internal abstract class TagCollectionIMPL_Base<T> : ReadOnlyTagCollectionIMPL_Base<T>, ITagCollection
    {
        public abstract bool TryAdd(EffectTag tag);
        public abstract bool TryAdd(EffectTagData data, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null);
        protected NotifyCollectionChangedEventHandler onChanged;

        public TagCollectionIMPL_Base(IList<T> tags) : base(tags)
        { }
        public TagCollectionIMPL_Base() : base()
        { }

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

        public void Clear()
        {
            tags.Clear();
        }

        public bool Remove(EffectTag tag)
        {
            int index = BinarySearchIndexOf(new TempIndices(tag.StartIndex, tag.OrderAtIndex));
            if (index < 0) return false;
            tags.RemoveAt(index);
            return true;
        }

        public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0)
        {
            int firstIndex = BinarySearchIndexOf(new StartIndexOnly(startIndex));
            if (firstIndex < 0) return 0;

            int lastIndex = firstIndex;

            do lastIndex++;
            while (lastIndex < tags.Count && Tag(lastIndex).StartIndex == startIndex);

            int count = lastIndex - firstIndex;
            if (buffer != null)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (bufferIndex < 0) throw new ArgumentOutOfRangeException(nameof(bufferIndex));

                int len = Mathf.Min(count, buffer.Length - bufferIndex);
                for (int i = 0; i < len; i++)
                {
                    buffer[bufferIndex + i] = Tag(firstIndex);
                    tags.RemoveAt(firstIndex);
                }
            }

            for (int i = 0; i < count; i++)
            {
                tags.RemoveAt(firstIndex);
            }

            return count;
        }

        public bool RemoveAt(int startIndex, int? order = null)
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
    }


    public class ReadOnlyTagCollection<T> : IReadOnlyTagCollection, IReadOnlyTagCollection<T> where T : ITagWrapper
    {
        private IReadOnlyTagCollection<T> collection;

        public int TagCount => collection.TagCount;
        public IEnumerable<EffectTag> Tags => collection.Tags;

        public ReadOnlyTagCollection(ITagCollection<T> collection)
        {
            this.collection = collection;
        }
        public ReadOnlyTagCollection(IList<T> list)
        {
            collection = new ReadOnlyTagCollectionIMPL<T>(list);
        }
        public ReadOnlyTagCollection() : base()
        {
            collection = new ReadOnlyTagCollectionIMPL<T>();
        }

        public IEnumerable<T> GetCached() => collection.GetCached();
        public IEnumerable<T> GetCached(int index) => collection.GetCached(index);
        public T GetCached(int index, int? order = null) => collection.GetCached(index, order);
        public T GetCached(EffectTag tag) => collection.GetCached(tag);

        public bool Contains(EffectTag item) => collection.Contains(item);

        public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0) => collection.TagsAt(startIndex, buffer, bufferIndex);
        public IEnumerable<EffectTag> TagsAt(int startIndex) => collection.TagsAt(startIndex);
        public EffectTag TagAt(int startIndex, int? order = null) => collection.TagAt(startIndex, order);
        public IEnumerator<EffectTag> GetEnumerator() => collection.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => collection.GetEnumerator();
    }

    public class ReadOnlyTagCollection : IReadOnlyTagCollection
    {
        public int TagCount => collection.TagCount;
        public IEnumerable<EffectTag> Tags => collection.Tags;

        private IReadOnlyTagCollection collection;

        public ReadOnlyTagCollection(ITagCollection collection)
        {
            this.collection = collection;
        }
        public ReadOnlyTagCollection(IList<EffectTag> list)
        {
            collection = new ReadOnlyTagCollectionIMPL(list);
        }
        public ReadOnlyTagCollection() : base()
        {
            collection = new ReadOnlyTagCollectionIMPL();
        }

        public bool Contains(EffectTag item) => collection.Contains(item);
        public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0) => collection.TagsAt(startIndex, buffer, bufferIndex);
        public IEnumerable<EffectTag> TagsAt(int startIndex) => collection.TagsAt(startIndex);
        public EffectTag TagAt(int startIndex, int? order = null) => collection.TagAt(startIndex, order);
        public IEnumerator<EffectTag> GetEnumerator() => collection.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => collection.GetEnumerator();
    }

    internal class ReadOnlyTagCollectionIMPL<T> : ReadOnlyTagCollectionIMPL_Base<T>, IReadOnlyTagCollection<T> where T : ITagWrapper
    {
        public ReadOnlyTagCollectionIMPL() : base()
        { }
        public ReadOnlyTagCollectionIMPL(IList<T> tags) : base(tags)
        { }

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

    internal class ReadOnlyTagCollectionIMPL : ReadOnlyTagCollectionIMPL_Base<EffectTag>
    {
        public ReadOnlyTagCollectionIMPL(IList<EffectTag> list) : base(list)
        { }
        public ReadOnlyTagCollectionIMPL() : base()
        { }

        protected override EffectTag Tag(int i) => tags[i];
    }

    internal abstract class ReadOnlyTagCollectionIMPL_Base<T> : IReadOnlyTagCollection
    {
        protected IList<T> tags;

        public ReadOnlyTagCollectionIMPL_Base(IList<T> tags)
        {
            this.tags = tags;
        }
        public ReadOnlyTagCollectionIMPL_Base()
        {
            this.tags = new List<T>();
        }

        // TODO Does unity c# compiler support devirtualization?
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected abstract EffectTag Tag(int i);

        public int TagCount => tags.Count;
        public IEnumerable<EffectTag> Tags
        {
            get
            {
                for (int i = 0; i < tags.Count; i++)
                    yield return Tag(i);
            }
        }

        public bool Contains(EffectTag tag)
        {
            int index = BinarySearchIndexOf(new TempIndices(tag.StartIndex, tag.OrderAtIndex));
            if (index < 0) return false;
            if (Tag(index) != tag) return false;
            return true;
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

            return Tag(index);
        }

        public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0)
        {
            int firstIndex = BinarySearchIndexOf(new StartIndexOnly(startIndex));
            if (firstIndex < 0) return 0;

            int lastIndex = firstIndex;

            do lastIndex++;
            while (lastIndex < tags.Count && Tag(lastIndex).StartIndex != startIndex);

            int count = lastIndex - firstIndex;
            if (buffer != null)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (bufferIndex < 0) throw new ArgumentOutOfRangeException(nameof(bufferIndex));

                int len = Mathf.Min(count, buffer.Length - bufferIndex);
                for (int i = 0; i < len; i++)
                {
                    buffer[bufferIndex + i] = Tag(firstIndex);
                }
            }

            return count;
        }

        public IEnumerable<EffectTag> TagsAt(int startIndex)
        {
            int firstIndex = BinarySearchIndexOf(new StartIndexOnly(startIndex));
            if (firstIndex < 0) yield break;

            int lastIndex = firstIndex;

            do yield return Tag(lastIndex++);
            while (lastIndex < tags.Count && Tag(lastIndex).StartIndex == startIndex);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        protected int BinarySearchIndexOf(IComparable<EffectTag> indices)
        {
            int lower = 0;
            int upper = tags.Count - 1;

            while (lower <= upper)
            {
                int middle = lower + (upper - lower) / 2;
                int comparisonResult = indices.CompareTo(Tag(middle));
                if (comparisonResult == 0)
                    return middle;
                else if (comparisonResult < 0)
                    upper = middle - 1;
                else
                    lower = middle + 1;
            }

            return ~lower;
        }

        protected struct TempIndices : IComparable<EffectTag>
        {
            private readonly int startIndex;
            private readonly int orderAtIndex;

            public TempIndices(int startIndex, int orderAtIndex)
            {
                this.startIndex = startIndex;
                this.orderAtIndex = orderAtIndex;
            }

            public int CompareTo(EffectTag other)
            {
                int res = startIndex.CompareTo(other.StartIndex);
                if (res == 0) return orderAtIndex.CompareTo(other.OrderAtIndex);
                return res;
            }
        }

        protected struct StartIndexOnly : IComparable<EffectTag>
        {
            private readonly int startIndex;

            public StartIndexOnly(int startIndex)
            {
                this.startIndex = startIndex;
            }

            public int CompareTo(EffectTag other)
            {
                return startIndex.CompareTo(other.StartIndex);
            }
        }

    }


    // TODO Potentially remove anything like this; decouple cacher and collection
    /// <summary>
    /// A writable collection of <see cref="ITagWrapper"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ITagCollection<out T> : ITagCollection, IReadOnlyTagCollection<T> where T : ITagWrapper
    {

    }

    /// <summary>
    /// A readonly collection of <see cref="ITagWrapper"/>.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReadOnlyTagCollection<out T> : IReadOnlyTagCollection where T : ITagWrapper
    {
        public IEnumerable<T> GetCached();
        public IEnumerable<T> GetCached(int index);
        public T GetCached(int index, int? order = null);
        public T GetCached(EffectTag tag);
    }




    /// <summary>
    /// A writable collection of tags.
    /// </summary>
    public interface ITagCollection : ICollection<EffectTag>, IReadOnlyTagCollection
    {
        public bool TryAdd(EffectTag tag);
        public bool TryAdd(EffectTagData data, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null);

        public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0);
        public bool RemoveAt(int startIndex, int? order = null);


        int ICollection<EffectTag>.Count => TagCount;
        void ICollection<EffectTag>.Add(EffectTag item)
        {
            if (!TryAdd(item)) throw new ArgumentException(nameof(item));
        }
        bool ICollection<EffectTag>.IsReadOnly => false;
    }

    /// <summary>
    /// A readonly collection of tags.
    /// </summary>
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
}
