using IntervalTree;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Security.Cryptography;
using TMPEffects.Components;
using TMPEffects.Tags;
using UnityEngine;

public interface ITagCacher<T> where T : ITagWrapper
{
    public T CacheTag(EffectTag tag, EffectTagIndices indices);
}

// TODO
// Better name for this?
public class CachedCollection<T> where T : ITagWrapper
{
    private IntervalTree<OrderedIndex, T> lol;
    private ObservableTagCollection tagCollection;
    private ITagCacher<T> cacher;

    public int Count => lol.Count;

    public CachedCollection(ITagCacher<T> cacher, ObservableTagCollection tagCollection)
    {
        id = new OrderedIndex(0, int.MaxValue);
        this.cacher = cacher;
        this.tagCollection = tagCollection;
        lol = new IntervalTree<OrderedIndex, T>(new OrderedIndexComparer());

        foreach (var tagData in tagCollection.TagsWithIndices)
        {
            Add(tagData.Key, tagData.Value);
        }

        tagCollection.CollectionChanged += OnCollectionChanged;
    }

    private void Add(EffectTagIndices indices, EffectTag tag)
    {
        lol.Add(new OrderedIndex(indices.StartIndex, indices.OrderAtIndex), new OrderedIndex(indices.EndIndex, indices.OrderAtIndex), cacher.CacheTag(tag, indices));
    }

    private void Remove(KeyValuePair<EffectTagIndices, EffectTag> kvp)
    {
        lol.RemoveWhere(x => x.From.index == kvp.Key.StartIndex && x.From.index == kvp.Key.OrderAtIndex && x.Value.Tag.Equals(kvp.Value));
    }

    private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems.Count > 0) Debug.LogWarning("Added more than one element; Should be impossible");
                KeyValuePair<EffectTagIndices, EffectTag> kvp = (KeyValuePair<EffectTagIndices, EffectTag>)e.NewItems[0];
                Add(kvp.Key, kvp.Value);
                break;

            case NotifyCollectionChangedAction.Remove:
                foreach (var item in e.OldItems)
                {
                    kvp = (KeyValuePair<EffectTagIndices, EffectTag>)item;
                    Remove(kvp);
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                lol.Clear();
                break;

            case NotifyCollectionChangedAction.Move:
                throw new NotImplementedException();

            case NotifyCollectionChangedAction.Replace:
                kvp = (KeyValuePair<EffectTagIndices, EffectTag>)e.OldItems[0];
                Remove(kvp);
                kvp = (KeyValuePair<EffectTagIndices, EffectTag>)e.NewItems[0];
                Add(kvp.Key, kvp.Value);
                break;
        }
    }

    private OrderedIndex id;
    public IEnumerable<T> GetCached(int index)
    {
        id.index = index;
        return lol.Query(id);
    }

    private struct OrderedIndex
    {
        public int index;
        public int order;     
        
        public OrderedIndex(int index, int order)
        {
            this.index = index;
            this.order = order;
        }
    }

    private class OrderedIndexComparer : IComparer<OrderedIndex>
    {
        public int Compare(OrderedIndex x, OrderedIndex y)
        {
            int res = (x.index.CompareTo(y.index));
            if (res == 0) return x.order.CompareTo(y.order);
            return res;
        }
    }
}