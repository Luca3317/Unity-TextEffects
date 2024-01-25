using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace TMPEffects.TextProcessing
{
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
}