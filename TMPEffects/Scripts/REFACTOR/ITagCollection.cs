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
    /// <summary>
    /// A writable collection of tags.
    /// </summary>
    public class TagCollectionOLD : ITagCollection
    {
        public int TagCount => collection.TagCount;
        public IEnumerable<EffectTag> Tags => collection.Tags;

        private ITagCollection collection;

        public TagCollection(ITagCollection collection)
        {
            this.collection = collection;
        }
        public TagCollection(List<EffectTag> list)
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

    internal class TagCollectionIMPL : TagCollectionIMPL_Base
    {
        public TagCollectionIMPL(List<EffectTag> list) : base(list)
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

    }

    internal abstract class TagCollectionIMPL_Base : ReadOnlyTagCollectionIMPL_Base, ITagCollection
    {
        public abstract bool TryAdd(EffectTag tag);
        public abstract bool TryAdd(EffectTagData data, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null);
        protected NotifyCollectionChangedEventHandler onChanged;

        public TagCollectionIMPL_Base(List<EffectTag> tags) : base(tags)
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
                array[arrayIndex + i] = tags[i];
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
            while (lastIndex < tags.Count && tags[lastIndex].StartIndex == startIndex);

            int count = lastIndex - firstIndex;
            if (buffer != null)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (bufferIndex < 0) throw new ArgumentOutOfRangeException(nameof(bufferIndex));

                int len = Mathf.Min(count, buffer.Length - bufferIndex);
                for (int i = 0; i < len; i++)
                {
                    buffer[bufferIndex + i] = tags[firstIndex];
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


    public class TagCollection : ITagCollection
    {
        protected List<EffectTag> tags;
        protected readonly ITMPTagValidator validator;

        public TagCollection(List<EffectTag> tags, ITMPTagValidator validator = null)
        {
            this.validator = validator;
            this.tags = tags;
        }
        public TagCollection(ITMPTagValidator validator = null)
        {
            this.validator = validator;
            this.tags = new List<EffectTag>();
        }


        public virtual bool TryAdd(EffectTag tag)
        {
            if (validator != null && !validator.ValidateTag(tag.Data)) return false;
            
            
        }

        public virtual bool TryAdd(EffectTagData data, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null)
        {
        }

        public virtual int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0)
        {
            int firstIndex = BinarySearchIndexOf(new StartIndexOnly(startIndex));
            if (firstIndex < 0) return 0;

            int lastIndex = firstIndex;

            do lastIndex++;
            while (lastIndex < tags.Count && tags[lastIndex].StartIndex == startIndex);

            int count = lastIndex - firstIndex;
            if (buffer != null)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (bufferIndex < 0) throw new ArgumentOutOfRangeException(nameof(bufferIndex));

                int len = Mathf.Min(count, buffer.Length - bufferIndex);
                for (int i = 0; i < len; i++)
                {
                    buffer[bufferIndex + i] = tags[firstIndex];
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

        public virtual void CopyTo(EffectTag[] array, int arrayIndex)
        {
            if (array is null) throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < tags.Count) throw new ArgumentException(nameof(array));

            for (int i = 0; i < tags.Count; i++)
            {
                array[arrayIndex + i] = tags[i];
            }
        }

        public virtual bool Remove(EffectTag tag)
        {
            int index = BinarySearchIndexOf(new TempIndices(tag.StartIndex, tag.OrderAtIndex));
            if (index < 0) return false;
            tags.RemoveAt(index);
            return true;
        }

        public int TagCount => tags.Count;
        public IEnumerable<EffectTag> Tags
        {
            get
            {
                for (int i = 0; i < tags.Count; i++)
                    yield return tags[i];
            }
        }

        public bool Contains(EffectTag tag)
        {
            int index = BinarySearchIndexOf(new TempIndices(tag.StartIndex, tag.OrderAtIndex));
            if (index < 0) return false;
            if (tags[index] != tag) return false;
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

            return tags[index];
        }

        public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0)
        {
            int firstIndex = BinarySearchIndexOf(new StartIndexOnly(startIndex));
            if (firstIndex < 0) return 0;

            int lastIndex = firstIndex;

            do lastIndex++;
            while (lastIndex < tags.Count && tags[lastIndex].StartIndex != startIndex);

            int count = lastIndex - firstIndex;
            if (buffer != null)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (bufferIndex < 0) throw new ArgumentOutOfRangeException(nameof(bufferIndex));

                int len = Mathf.Min(count, buffer.Length - bufferIndex);
                for (int i = 0; i < len; i++)
                {
                    buffer[bufferIndex + i] = tags[firstIndex];
                }
            }

            return count;
        }

        public IEnumerable<EffectTag> TagsAt(int startIndex)
        {
            int firstIndex = BinarySearchIndexOf(new StartIndexOnly(startIndex));
            if (firstIndex < 0) yield break;

            int lastIndex = firstIndex;

            do yield return tags[lastIndex++];
            while (lastIndex < tags.Count && tags[lastIndex].StartIndex == startIndex);
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
                int comparisonResult = indices.CompareTo(tags[middle]);
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

    public class ReadOnlyTagCollection : IReadOnlyTagCollection
    {
        private IReadOnlyTagCollection collection;

        internal ReadOnlyTagCollection(List<EffectTag> tags)
        {
            this.collection = new TagCollection(tags);
        }

        internal ReadOnlyTagCollection(IReadOnlyTagCollection collection)
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
            return collection.GetEnumerator();
        }
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
