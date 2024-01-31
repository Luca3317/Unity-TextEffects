using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

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
        Removed = 10,
        Reset = 20
    }

    public delegate void NotifyProcessorsChangedEventHandler(object sender, NotifyProcessorsChangedEventArgs e);

    public interface INotifyProcessorsChanged
    {
        event NotifyProcessorsChangedEventHandler ProcessorsChanged;
    }

    internal interface ITagProcessorManager : IEnumerable<TagProcessor>, INotifyProcessorsChanged
    {
        public ReadOnlyDictionary<char, ReadOnlyCollection<TagProcessor>> TagProcessors { get; }

        public void AddProcessor(char prefix, TagProcessor processor, int priority = 0);
        public bool RemoveProcessor(char prefix, TagProcessor processor);
    }

    // TODO
    // Maybe remove the entire inotifyprocessorschanged?
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

        public void AddProcessor(char prefix, TagProcessor processor, int priority = 0)
        {
            if (processor == null) throw new System.ArgumentNullException(nameof(processor));

            List<TagProcessor> processors;
            if (tagProcessors.TryGetValue(prefix, out processors))
            {
                if (priority > processors.Count || priority < 0)
                {
                    processors.Add(processor);
                    ProcessorsChanged?.Invoke(this, new NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction.Added, prefix, processors.Count));
                }
                else
                {
                    processors.Insert(priority, processor);
                    ProcessorsChanged?.Invoke(this, new NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction.Added, prefix, priority));
                }
            }
            else
            {
                processors = new List<TagProcessor>() { processor };
                tagProcessors.Add(prefix, processors);
                tagProcessorsRO.Add(prefix, new ReadOnlyCollection<TagProcessor>(processors));
                ProcessorsChanged?.Invoke(this, new NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction.Added, prefix, 0));
            }
        }

        public bool RemoveProcessor(char prefix, TagProcessor processor)
        {
            if (processor == null) throw new System.ArgumentNullException(nameof(processor));

            List<TagProcessor> processors;
            if (!tagProcessors.TryGetValue(prefix, out processors)) return false;

            int index = processors.IndexOf(processor);
            if (!processors.Remove(processor)) return false;

            ProcessorsChanged?.Invoke(this, new NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction.Removed, prefix, index));

            if (processors.Count == 0)
            {
                tagProcessors.Remove(prefix);
                tagProcessorsRO.Remove(prefix);
            }

            return true;
        }

        public void Clear()
        {
            tagProcessors.Clear();
            tagProcessorsRO.Clear();
            ProcessorsChanged?.Invoke(this, new NotifyProcessorsChangedEventArgs(NotifyProcessorsChangedAction.Reset));
        }

        public void RegisterTo(TMPTextProcessor textProcessor)
        {
            foreach (var kvp in tagProcessors)
            {
                foreach (var processor in kvp.Value)
                {
                    textProcessor.AddProcessor(kvp.Key, processor);
                }
            }
        }

        public void UnregisterFrom(TMPTextProcessor textProcessor)
        {
            foreach (var kvp in tagProcessors)
            {
                foreach (var processor in kvp.Value)
                {
                    textProcessor.RemoveProcessor(kvp.Key, processor);
                }
            }
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