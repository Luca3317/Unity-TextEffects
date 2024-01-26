using IntervalTree;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TMPEffects.Tags
{

    public interface IReadOnlyTagManager<TKey> : IReadOnlyTagCollection
    {
        public IReadOnlyTagCollection this[TKey key] { get; }
        public IEnumerable<TKey> Keys { get; }
        public int KeyCount { get; }
        public bool ContainsKey(TKey key);
    }

    public interface ITagManager<TKey> : IReadOnlyTagManager<TKey>, ITagCollection
    {
        public new ITagCollection this[TKey key] { get; }

        public void AddKey(TKey key);
        public bool RemoveKey(TKey key);
    }


    /*
     * Which parts are hard to generalize
     * 
     *      - exposed dictionary: TagCollection v ReadOnlyTagCollection v TagCollection<T> v ReadOnlyCollection<T>
     */


    internal class ObservableList<T> : IList<T>, INotifyCollectionChanged
    {
        private IList<T> list;

        public T this[int index] { get => list[index]; set => list[index] = value; }

        public int Count => list.Count;

        public bool IsReadOnly => list.IsReadOnly;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ObservableList()
        {
            list = new List<T>();
        }

        public void Add(T item)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item));
            list.Add(item);
        }

        public void Clear()
        {
            if (CollectionChanged == null) list.Clear();
            else
            {
                List<T> copy = new List<T>(list);
                list.Clear();
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset, copy));
            }
        }

        public bool Contains(T item)
        {
            return list.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            list.CopyTo(array, arrayIndex);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return list.GetEnumerator();
        }

        public int IndexOf(T item)
        {
            return list.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            list.Insert(index, item);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        public bool Remove(T item)
        {
            if (CollectionChanged == null) return list.Remove(item);
            bool removed = list.Remove(item);
            if (removed)
            {
                CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
            }
            return removed;
        }

        public void RemoveAt(int index)
        {
            if (CollectionChanged == null) list.RemoveAt(index);
            T item = list[index];
            list.RemoveAt(index);
            CollectionChanged(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)list).GetEnumerator();
        }
    }

    public class TagManager<TKey, TCached> : ITagManager<TKey> where TKey : ITMPPrefixSupplier where TCached : ITagWrapper
    {
        ITagCollection union;

        private Dictionary<TKey, ObservableList<EffectTag>> observables;
        private Dictionary<TKey, TagCollection> exposed;
        private Dictionary<char, TKey> prefixToKey;

        public TagCollection<TCached> AddKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (exposed.ContainsKey(key)) throw new ArgumentException(nameof(key));

            ObservableList<EffectTag> collection = new ObservableList<EffectTag>();
            collection.CollectionChanged += OnCollectionChanged;
            observables[key] = collection;

            exposed[key] = new TagCollection(collection);
            prefixToKey[key.Prefix] = key; 

            return collection;
        }

        public bool RemoveKey(TKey key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!exposed.ContainsKey(key)) return false;

            observables[key].CollectionChanged -= OnCollectionChanged;

            foreach (var tag in exposed[key])
            {
                union.Remove(tag);
            }

            observables.Remove(key);
            exposed.Remove(key);
            prefixToKey.Remove(key.Prefix);
            return true;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    // You can always only add one element at a time, so this direct access is fine
                    // This may fail however; if the new tag's indices are identical to another
                    // tag's indices

                    // TODO
                    // Maybe change TryAdd to accept tags that share the same start and order index
                    // Either:
                    //      Insert at the position where identical indices are and increment every following orderAtIndex
                    // or   Simply insert at that point and dont manipulate orderAtIndex; anything that works with tags will have to work with that changed concept
                    //
                    // Likely prefer the former; otherwise undefined when what tag will be applied (issue for e.g. event and command tags; which is first? Sometimes critical)
                    // But; dont think there is a way to make that work with direct tagcollection manipulation?

                    // New solutions
                    // Either:
                    //      Simply insert at that point (basically old 2nd solution)
                    //      

                    bool added = union.TryAdd((EffectTag)args.NewItems[0]);
                    if (added) return;
                    ((ObservableCollection<TCached>)sender).RemoveAt(args.NewStartingIndex);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    for (int i = 0; i < args.OldItems.Count; i++)
                    {
                        union.Remove((EffectTag)args.OldItems[i]);
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:





            }


        }

        public ITagCollection<TCached> this[TKey key] => exposed[key];
        public IEnumerable<TKey> Keys => exposed.Keys;
        public int KeyCount => exposed.Count;
        public bool ContainsKey(TKey key) => exposed.ContainsKey(key);
        IReadOnlyTagCollection<TCached> IReadOnlyTagManager<TKey, TCached>.this[TKey key] => exposed[key];
        IReadOnlyTagCollection IReadOnlyTagManager<TKey>.this[TKey key] => exposed[key];
        ITagCollection ITagManager<TKey>.this[TKey key] => exposed[key];

        public void Clear()
        {
            List<TKey> keys = exposed.Keys.ToList();
            foreach (var key in keys)
            {
                RemoveKey(key);
            }
        }

        public bool Remove(EffectTag item)
        {
            if (!union.Remove(item)) return false;

            exposed[prefixToKey[item.Prefix]].Remove(item);

            return union.Remove(item);
        }

        public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0)
        {
            int removed = union.RemoveAllAt(startIndex, buffer, bufferIndex);
            if (removed == 0) return 0;

            foreach (var key in exposed.Keys)
            {
                exposed[key].RemoveAllAt(startIndex);
            }

            return removed;
        }

        public bool RemoveAt(int startIndex, int? order = null)
        {
            EffectTag tag = union.TagAt(startIndex, order);
            if (tag == null) return false;
            return Remove(tag);
        }

        public bool TryAdd(EffectTag tag)
        {
            if (!exposed[prefixToKey[tag.Prefix]].TryAdd(tag)) return false;
            union.TryAdd(tag);
            return true;
        }

        public bool TryAdd(EffectTagData data, int startIndex = 0, int endIndex = -1, int? orderAtIndex = null)
        {
            throw new NotImplementedException();
        }

        public int TagCount => union.TagCount;
        public IEnumerable<EffectTag> Tags => union.Tags;
        public bool Contains(EffectTag item) => union.Contains(item);
        public void CopyTo(EffectTag[] array, int arrayIndex) => union.CopyTo(array, arrayIndex);
        public IEnumerable<TCached> GetCached() => union.GetCached();
        public IEnumerable<TCached> GetCached(int index) => union.GetCached(index);
        public TCached GetCached(int index, int? order = null) => union.GetCached(index, order);
        public TCached GetCached(EffectTag tag) => union.GetCached(tag);
        public IEnumerator<EffectTag> GetEnumerator() => union.GetEnumerator();
        public EffectTag TagAt(int startIndex, int? order = null) => union.TagAt(startIndex, order);
        public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0) => union.TagsAt(startIndex, buffer, bufferIndex);
        public IEnumerable<EffectTag> TagsAt(int startIndex) => union.TagsAt(startIndex);
        IEnumerator IEnumerable.GetEnumerator() => union.GetEnumerator();
    }
}