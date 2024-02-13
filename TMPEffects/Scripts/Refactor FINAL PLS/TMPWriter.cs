using AYellowpaper.SerializedCollections;
using System.Collections;
using TMPEffects.Commands;
using TMPEffects.Databases;
using TMPEffects.TextProcessing;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using System;
using UnityEditor;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using TMPEffects.Extensions;

namespace TMPEffects.Components
{

    /// <summary>
    /// Shows / hides the characters of a <see cref="TMP_Text"/> component over time.
    /// </summary>
    /// <remarks>
    /// One of the two main components of TMPEffects, along with <see cref="TMPAnimator"/>.<br/>
    /// TMPWriter allows you to show / hide text over time.<br/>  
    /// Using command tags, you can call specific methods. There are two types of Commands:
    /// <list type="table">
    /// <item><see cref="TMPCommand"/>: These are defined by the <see cref="TMPCommandDatabase"/> object on the component. As they derive from <see cref="ScriptableObject"/>, they are stored on disk. All built-in commands of this type serve to control the TMPWriter component.</item>
    /// <item><see cref="SceneCommand"/>: These are defined as a property on the TMPWriter component. You can use them to reference specific methods on objects in the scene.</item>
    /// </list>
    /// In additon to command tags, TMPWriter also processes event tags:<br/><br/>
    /// <see cref="TMPEvent"/>: Using event tags, you can raise events from text, i.e. when a specific character is shown. You can subscribe to these events with OnTextEvent. 
    /// </remarks>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    public class TMPWriter : TMPEffectComponent
    {
        // State
        public bool IsWriting => writing;
        public bool MaySkip => currentMaySkip;
        public int CurrentIndex => currentIndex;

        // Database
        public TMPCommandDatabase Database => database;

        // Tags
        public ITagCollection Tags => tags;
        public ITagCollection CommandTags => tags[commandCategory];
        public ITagCollection EventTags => tags[eventCategory];

        // Events
        public TMPEvent OnTextEvent;
        public UnityEvent<CharData> OnShowCharacter;
        public UnityEvent OnStartWriter;
        public UnityEvent OnStopWriter;
        public UnityEvent OnFinishWriter;
        public UnityEvent OnSkipWriter;
        public UnityEvent<int> OnResetWriter;

        #region Fields
        // Settings
        [SerializeField] TMPCommandDatabase database;

        [Tooltip("The speed at which the writer shows new characters.")]
        [SerializeField] float delay = 0.075f;

        [Tooltip("Whether the text may be skipped by default.")]
        [SerializeField] bool maySkip = false;

        [Tooltip("If checked, the writer will begin writing when it is first enabled. If not checked, you will have to manually start the writer from your own code.")]
        [SerializeField] bool writeOnStart = true;
        [Tooltip("If checked, the writer will automatically begin writing when the text on the associated TMP_Text component is modified. If not checked, you will have to manually start the writer from your own code.")]
        [SerializeField] bool autoWriteNewText = true;

        [Tooltip("The delay after whitespace characters, as percentage of the general delay")]
        [SerializeField, Range(0, 100)] float whiteSpaceDelay;
        [Tooltip("The delay after punctuation characters, as percentage of the general delay")]
        [SerializeField, Range(0, 100)] float punctuationDelay;
        [Tooltip("The delay after already visible characters, as percentage of the general delay")]
        [SerializeField, Range(0, 100)] float visibleDelay;
        [Tooltip("The delay after linebreaks, as percentage of the general delay")]
        [SerializeField, Range(0, 100)] float linebreakDelay;

        // Scene commands
        [Tooltip("Commands that may reference scene objects.\nNOT raised in preview mode.")]
        [SerializeField, SerializedDictionary("Tag Name", "Command")] SerializedDictionary<string, SceneCommand> sceneCommands;

        [System.NonSerialized] private TagProcessorManager processors;
        [System.NonSerialized] private TagCollectionManager<TMPEffectCategory> tags;

        [System.NonSerialized] private TMPCommandCategory commandCategory;
        [System.NonSerialized] private TMPEventCategory eventCategory;

        [System.NonSerialized] private CachedCollection<CachedCommand> commands;
        [System.NonSerialized] private CachedCollection<CachedEvent> events;

        [System.NonSerialized] private Coroutine writerCoroutine = null;
        [System.NonSerialized] private float currentDelay;
        [System.NonSerialized] private bool currentMaySkip;

        [System.NonSerialized] private bool shouldWait = false;
        [System.NonSerialized] private float waitAmount = 0f;
        [System.NonSerialized] private Func<bool> continueConditions;

        [System.NonSerialized] private bool writing = false;
        [System.NonSerialized] private int currentIndex = -1;
        #endregion

        #region Initialization

        private void OnEnable()
        {
            SubscribeToMediator();

            PrepareForProcessing();

            Mediator.ForceReprocess();

#if UNITY_EDITOR
            // TODO Probably should only happen when actually writing
            if (!Application.isPlaying) EditorApplication.update += EditorUpdate;
#endif
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            if (writeOnStart) StartWriter();
        }

        private void OnDisable()
        {
            processors.UnregisterFrom(Mediator.Processor);

            Mediator.ForceReprocess();

            UnsubscribeFromMediator();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
                StopWriterCoroutine();
                currentIndex = -1;
                Show(0, Mediator.CharData.Count, true);
                writing = false;
            }
#endif

            FreeMediator();
        }

        private void PrepareForProcessing()
        {
            CommandDatabase db = new CommandDatabase(database, sceneCommands);

            // Reset categories
            commandCategory = new TMPCommandCategory(ParsingUtility.COMMAND_PREFIX, db);
            eventCategory = new TMPEventCategory(ParsingUtility.EVENT_PREFIX);

            // Reset tagcollection & cachedcollection
            ReadOnlyCollection<CharData> ro = new ReadOnlyCollection<CharData>(Mediator.CharData);
            tags = new();
            commands = new CachedCollection<CachedCommand>(new CommandCacher(ro, this, commandCategory), tags.AddKey(commandCategory));
            events = new CachedCollection<CachedEvent>(new EventCacher(/*ro,*/ OnTextEvent), tags.AddKey(eventCategory));

            // Reset processors
            processors ??= new();
            processors.UnregisterFrom(Mediator.Processor);
            processors.Clear();

            processors.AddProcessor(commandCategory.Prefix, new TagProcessor(commandCategory));
            processors.AddProcessor(eventCategory.Prefix, new TagProcessor(eventCategory));

            processors.RegisterTo(Mediator.Processor);
        }

        private void SubscribeToMediator()
        {
            Mediator.TextChanged += OnTextChanged;
            Mediator.Processor.FinishAdjustIndeces += PostProcessTags;
        }

        private void UnsubscribeFromMediator()
        {
            Mediator.TextChanged -= OnTextChanged;
            Mediator.Processor.FinishAdjustIndeces -= PostProcessTags;
        }

        private void OnDatabaseChanged()
        {
            PrepareForProcessing();
        }
        #endregion

        #region Writer Controlling
        /// <summary>
        /// Start (or resume) writing.
        /// </summary>
        public void StartWriter()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;

            if (!writing)
            {
                StartWriterCoroutine();
            }

            OnStartWriter?.Invoke();
        }

        /// <summary>
        /// Stop writing. 
        /// Note that this does not reset the shown text.
        /// </summary>
        public void StopWriter()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;

            if (writing)
            {
                StopWriterCoroutine();
            }

            OnStopWriter?.Invoke();
        }

        /// <summary>
        /// Reset the writer.
        /// </summary>
        public void ResetWriter()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;

            if (writing)
            {
                StopWriterCoroutine();
            }

            // reset
            currentIndex = -1;
            ResetInvokables(Mediator.CharData.Count);
            Hide(0, Mediator.CharData.Count, true);

            ResetData();

            OnResetWriter?.Invoke(0);
        }

        /// <summary>
        /// Reset the writer to the given index.
        /// </summary>
        /// <param name="index">The index to reset the writer to.</param>
        public void ResetWriter(int index)
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return;

            if (writing)
            {
                StopWriterCoroutine();
            }

            // reset
            ResetData();

            Hide(index, Mediator.CharData.Count - index, true);
            Show(0, index, true);

            ResetInvokables(currentIndex);
            for (int i = -1; i < index; i++)
            {
                RaiseInvokables(i);
            }

            currentIndex = index;

            OnResetWriter?.Invoke(index);
        }

        /// <summary>
        /// Finish the writing process. 
        /// This will instantly show all characters, raise all not-yet raised events and execute commands that are tagged with Raise-On-Skip.
        /// </summary>
        public void FinishWriter()
        {
            if (!isActiveAndEnabled || gameObject.activeInHierarchy || !currentMaySkip) return;

            int skipTo;
            CachedCommand cc = commands.FirstOrDefault(x => x.Indices.StartIndex >= currentIndex && x.Tag.Name == "skippable" && x.Tag.Parameters != null && x.Tag.Parameters[""] == "false");

            if (cc == default) skipTo = Mediator.CharData.Count;
            else skipTo = cc.Indices.StartIndex;

            OnSkipWriter?.Invoke();

            for (int i = currentIndex; i < skipTo; i++)
            {
                RaiseInvokables(i, true);
            }

            currentIndex = skipTo;
            Show(0, skipTo, true); // TODO toggle for whether to skip show animations on skip

            if (skipTo == Mediator.CharData.Count)
            {
                if (writing) StopWriterCoroutine();
                OnFinishWriter?.Invoke();
            }
        }

        /// <summary>
        /// Restart the writer.
        /// This will reset the writer and start the writing process.
        /// </summary>
        public void RestartWriter()
        {
            if (!isActiveAndEnabled) return;

            ResetWriter();
            StartWriter();
        }
        #endregion

        #region Writer Commands
        /// <summary>
        /// Pause the writer for the given amount of seconds.
        /// </summary>
        /// <param name="seconds">The amount of time to wait.</param>
        /// <exception cref="System.ArgumentOutOfRangeException">Throws if <paramref name="seconds"/> is less than zero.</exception>
        public void Wait(float seconds)
        {
            if (seconds < 0f)
            {
                throw new System.ArgumentOutOfRangeException("Seconds was negative");
            }

            if (shouldWait)
            {
                waitAmount = Mathf.Max(waitAmount, seconds);
                return;
            }

            shouldWait = true;
            waitAmount = seconds;
        }

        public void WaitUntil(Func<bool> condition)
        {
            if (condition == null) return;

            continueConditions -= condition;
            continueConditions += condition;
        }

        /// <summary>
        /// Set the current speed of the writer.
        /// </summary>
        /// <param name="speed">The speed to set the writer to.</param>
        public void SetSpeed(float speed)
        {
            currentDelay = speed;
        }
        #endregion

        #region Setters
        /// <summary>
        /// Set whether the current text may be skipped.
        /// </summary>
        /// <param name="skippable"></param>
        public void SetSkippable(bool skippable)
        {
            currentMaySkip = skippable;
        }

        /// <summary>
        /// Set the database that will be used to parse command tags.
        /// </summary>
        /// <param name="database">The database that will be used to parse command tags.</param>
        public void SetDatabase(TMPCommandDatabase database)
        {
            this.database = database;
            OnDatabaseChanged();
        }
        #endregion

        #region Event Callbacks
        private void OnTextChanged()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                bool wasWriting = writing;
                ResetWriter();
                if (wasWriting)
                {
                    StartWriter();
                }
                else Show(0, Mediator.CharData.Count, true);
                return;
            }
#endif

            ResetWriter();
            if (autoWriteNewText) StartWriter();
        }
        #endregion

        #region Writer Coroutine
        private void StartWriterCoroutine()
        {
            StopWriterCoroutine();
            writerCoroutine = StartCoroutine(WriterCoroutine());
        }

        private void StopWriterCoroutine()
        {
            if (writerCoroutine != null)
            {
                StopCoroutine(writerCoroutine);
                writerCoroutine = null;
            }

            OnStopWriting();
        }

        private IEnumerator WriterCoroutine()
        {
            OnStartWriting();

            // TODO This indicates the text is fully shown already
            if (currentIndex >= Mediator.CharData.Count)
            {
                OnStopWriting();
                yield break;
            }

            // Reset all relevant variables
            if (currentIndex <= 0) ResetData();

            if (currentIndex == -1)
                HideAllCharacters(true);

            // Execute instant commands
            yield return RaiseInvokablesCoroutine(-1);

            CharData cData;
            for (int i = Mathf.Max(currentIndex, 0); i < Mediator.CharData.Count; i++)
            {
                currentIndex = i;
                cData = Mediator.CharData[i];

                // Execute the commands & events for the current index
                yield return RaiseInvokablesCoroutine(i);

                // Calculate delay; do here to use accurate visibilitystate
                float delay = CalculateDelay(i);

                // Show the current character
                OnShowCharacter?.Invoke(cData);
                Show(i, 1, false);
                Mediator.ForceUpdate(i, 1);

                // Calculate and wait for the delay for the current index
                if (delay > 0) yield return new WaitForSeconds(delay);
            }

            OnFinishWriter?.Invoke();
            OnStopWriting();
        }

        private void ResetData()
        {
            currentDelay = delay;
            currentMaySkip = maySkip;
        }

        private void ResetInvokables(int maxIndex)
        {
            foreach (var eventt in events)
            {
                if ((eventt.Indices.StartIndex < maxIndex || eventt.ExecuteInstantly) && eventt.ExecuteRepeatable) eventt.Reset();
            }

            foreach (var command in commands)
            {
                if ((command.Indices.StartIndex < maxIndex || command.ExecuteInstantly) && command.ExecuteRepeatable) command.Reset();
            }
        }

        private float CalculateDelay(int index)
        {
            if (currentDelay <= 0) return 0;

            CharData cData = Mediator.CharData[index];

            // If character is invisible (=> is whitespace)
            if (!cData.info.isVisible) // Alternatively check char.IsWhiteSpace
            {
                // If line break
                if (index < Mediator.CharData.Count - 1 && Mediator.CharData[index + 1].info.lineNumber > Mediator.CharData[index].info.lineNumber)
                {
                    return Mathf.Max(currentDelay * linebreakDelay);
                }

                return Mathf.Max(currentDelay * whiteSpaceDelay, 0);
            }
            // If character is already shown (e.g. through using the !show command)
            if (cData.visibilityState == CharData.VisibilityState.Shown || cData.visibilityState == CharData.VisibilityState.ShowAnimation)
            {
                return Mathf.Max(currentDelay * visibleDelay, 0);
            }
            // If character is punctuation, and not directly followed by another punctuation (to multiple extended delays for e.g. "..." or "?!?"
            if (char.IsPunctuation(cData.info.character) && (index == Mediator.CharData.Count - 1 || !char.IsPunctuation(Mediator.CharData[index + 1].info.character)))
            {
                return Mathf.Max(currentDelay * punctuationDelay, 0);
            }

            return currentDelay;
        }

        // TODO Toggle for whether to wait 
        //      Until all conditions have been true at one point since starting the check
        //      Until all conditions are true at once
        private IEnumerator HandleWaitConditions()
        {
            if (continueConditions == null) yield break;

            bool allMet;
            Delegate[] delegates;
            do
            {
                allMet = true;
                delegates = continueConditions.GetInvocationList();

                for (int i = 0; i < delegates.Length; i++)
                {
                    if (!(delegates[i] as Func<bool>)()) allMet = false;
                    else continueConditions -= (delegates[i] as Func<bool>);
                }

                yield return null;

            } while (!allMet);

            continueConditions = null;
        }

        private IEnumerable<ICachedInvokable> GetInvokables(int index, bool skipped = false)
        {
            IEnumerable<ICachedInvokable> invokables;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (!commandsEnabled && !eventsEnabled) return new List<ICachedInvokable>();


                if (skipped)
                {
                    if (commandsEnabled)
                    {
                        invokables = commands;
                        if (eventsEnabled)
                        {
                            invokables = invokables.Concat(events);
                            invokables = invokables.OrderBy(x => x.Indices.StartIndex).ThenBy(x => x.Indices.OrderAtIndex);
                        }
                    }
                    else { invokables = events; }

                    invokables = invokables.Where(x => x.Indices.StartIndex >= index && x.ExecuteOnSkip);
                }
                else if (index < 0)
                {
                    if (commandsEnabled)
                    {
                        invokables = commands;
                        invokables = invokables.Where(x => x.ExecuteInstantly);
                    }
                    else invokables = new List<ICachedInvokable>();

                }
                else
                {
                    if (commandsEnabled)
                    {
                        invokables = commands.GetAt(index);
                        if (eventsEnabled)
                        {
                            invokables = invokables.Concat(events.GetAt(index));
                            invokables = invokables.OrderBy(x => x.Indices.StartIndex).ThenBy(x => x.Indices.OrderAtIndex);
                        }
                    }
                    else invokables = events.GetAt(index);
                }

                return invokables.Where(x => x.ExecuteInPreview);
            }
#endif

            if (skipped)
            {
                invokables = commands;
                if (events.HasAny())
                {
                    invokables = invokables.Concat(events);
                    invokables = invokables.OrderBy(x => x.Indices.StartIndex).ThenBy(x => x.Indices.OrderAtIndex);
                }
                invokables = invokables.Where(x => x.Indices.StartIndex >= index && x.ExecuteOnSkip);
            }
            else if (index < 0)
            {
                invokables = commands;
                invokables = invokables.Where(x => x.ExecuteInstantly);
            }
            else
            {
                invokables = commands.GetAt(index);
                if (events.HasAnyAt(index))
                {
                    invokables = invokables.Concat(events.GetAt(index));
                    invokables = invokables.OrderBy(x => x.Indices.StartIndex).ThenBy(x => x.Indices.OrderAtIndex);
                }
            }

            return invokables;
        }

        private void RaiseInvokables(int index, bool skipped = false)
        {
            foreach (var invokable in GetInvokables(index, skipped))
            {
                invokable.Trigger();
                waitAmount = 0f;
                shouldWait = false;
                continueConditions = null;
            }
        }

        private IEnumerator RaiseInvokablesCoroutine(int index, bool skipped = false, bool block = true)
        {
            foreach (var invokable in GetInvokables(index, skipped))
            {
                invokable.Trigger();

                if (block)
                {
                    if (shouldWait) yield return new WaitForSeconds(waitAmount);
                    yield return HandleWaitConditions();
                }

                waitAmount = 0f;
                shouldWait = false;
                continueConditions = null;
            }
        }

        private void OnStopWriting()
        {
            writing = false;
        }

        private void OnStartWriting()
        {
            writing = true;
        }
        #endregion

        #region Editor
#if UNITY_EDITOR
        [System.NonSerialized] TMPCommandDatabase prevDatabase = null;
        [System.NonSerialized] bool reprocessFlag = false;
        [SerializeField, HideInInspector] bool eventsEnabled = false;
        [SerializeField, HideInInspector] bool commandsEnabled = false;

        private void EditorUpdate()
        {
            if (reprocessFlag)
            {
                Mediator.ForceReprocess();
                reprocessFlag = false;
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }

        [MenuItem("CONTEXT/TMP_Text/Add writer")]
        static void AddWriter(MenuCommand command)
        {
            TMP_Text text = command.context as TMP_Text;
            if (text == null)
            {
                Debug.LogWarning("Could not add writer to " + command.context.name);
                return;
            }

            text.gameObject.GetOrAddComponent<TMPWriter>();
        }

        private void OnValidate()
        {
            // Ensure data is set - OnValidate called before OnEnable
            if (MediatorThreadSafe == null || !Mediator.isInitialized) return;

            if (database != prevDatabase)
            {
                prevDatabase = database;
                OnDatabaseChanged();

                reprocessFlag = true;
            }
        }

        public void ForceReprocess()
        {
            Mediator.ForceReprocess();
        }
#endif
        #endregion

        private void HideAllCharacters(bool skipAnimations = false)
        {
            Hide(0, Mediator.CharData.Count, skipAnimations);
        }

        private void PostProcessTags(string text)
        {
            tags.Clear();

            foreach (var processor in processors.TagProcessors[commandCategory.Prefix])
            {
                foreach (var tag in processor.ProcessedTags)
                {
                    tags[commandCategory].TryAdd(tag.Value, tag.Key);
                }
            }

            foreach (var processor in processors.TagProcessors[eventCategory.Prefix])
            {
                foreach (var tag in processor.ProcessedTags)
                    tags[eventCategory].TryAdd(tag.Value, tag.Key);
            }
        }

        public void Show(int start, int length, bool skipAnimation = false)
        {
            TMP_TextInfo info = Mediator.Text.textInfo;
            if (start < 0 || length < 0 || start + length > info.characterCount)
            {
                throw new System.ArgumentException("Invalid input: Start = " + start + "; Length = " + length + "; Length of string: " + info.characterCount);
            }

            CharData cData;
            TMP_CharacterInfo cInfo;
            //if (skipAnimation)
            //{
            Vector3[] verts;
            Color32[] colors;
            int vIndex, mIndex;

            for (int i = start; i < start + length; i++)
            {
                cInfo = info.characterInfo[i];
                cData = Mediator.CharData[i];
                if (!cData.info.isVisible) continue;

                // Set the current mesh's vertices all to the initial mesh values
                for (int j = 0; j < 4; j++)
                {
                    cData.SetVertex(j, cData.mesh.initial.GetPosition(j));
                }

                CharData.VisibilityState prev = cData.visibilityState;
                cData.SetVisibilityState(skipAnimation ? CharData.VisibilityState.Shown : CharData.VisibilityState.ShowAnimation, Time.time); // TODO What time value to use here?

                // Apply the new vertices to the vertex array
                vIndex = cInfo.vertexIndex;
                mIndex = cInfo.materialReferenceIndex;

                colors = info.meshInfo[mIndex].colors32;
                verts = info.meshInfo[mIndex].vertices;

                for (int j = 0; j < 4; j++)
                {
                    verts[vIndex + j] = cData.mesh[j].position;
                    //colors[vIndex + j] = cData.mesh[j].color;
                }

                // Apply the new vertices to the char data array
                Mediator.CharData[i] = cData;
                Mediator.VisibilityStateUpdated(i, prev);
            }

            if (Mediator.Text.mesh != null)
                Mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);

            Mediator.ForceUpdate(start, length);
        }

        public void Hide(int start, int length, bool skipAnimation = false)
        {
            TMP_TextInfo info = Mediator.Text.textInfo;
            if (start < 0 || length < 0 || start + length > info.characterCount)
            {
                throw new System.ArgumentException("Invalid input: Start = " + start + "; Length = " + length + "; Length of string: " + info.characterCount);
            }

            CharData cData;
            TMP_CharacterInfo cInfo;
            //if (skipAnimation)
            //{
            Vector3[] verts;
            Color32[] colors;
            int vIndex, mIndex;

            for (int i = start; i < start + length; i++)
            {
                cInfo = info.characterInfo[i];
                if (!cInfo.isVisible) continue;

                cData = Mediator.CharData[i];

                // Set the current mesh's vertices all to the initial mesh values
                for (int j = 0; j < 4; j++)
                {
                    cData.SetVertex(j, cData.info.initialPosition);
                }

                CharData.VisibilityState prev = cData.visibilityState;
                cData.SetVisibilityState(skipAnimation ? CharData.VisibilityState.Hidden : CharData.VisibilityState.HideAnimation, Time.time); // TODO What time value to use here?

                // Apply the new vertices to the vertex array
                vIndex = cInfo.vertexIndex;
                mIndex = cInfo.materialReferenceIndex;

                colors = info.meshInfo[mIndex].colors32;
                verts = info.meshInfo[mIndex].vertices;

                for (int j = 0; j < 4; j++)
                {
                    verts[vIndex + j] = cData.mesh[j].position;
                    //colors[vIndex + j] = cData.mesh[j].color;
                }

                // Apply the new vertices to the char data array
                Mediator.CharData[i] = cData;
                Mediator.VisibilityStateUpdated(i, prev);
            }

            if (Mediator.Text.mesh != null)
                Mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);

            Mediator.ForceUpdate(start, length);
        }
    }





    internal class CommandDatabase : ITMPEffectDatabase<ITMPCommand>
    {
        private ITMPEffectDatabase<ITMPCommand> database;
        private IDictionary<string, SceneCommand> sceneDatabase;

        public CommandDatabase(ITMPEffectDatabase<ITMPCommand> database, IDictionary<string, SceneCommand> sceneDatabase)
        {
            this.database = database;
            this.sceneDatabase = sceneDatabase;
        }

        public bool ContainsEffect(string name)
        {
            bool contains = database != null && database.ContainsEffect(name);
            if (contains) return true;
            return sceneDatabase != null && sceneDatabase.ContainsKey(name);
        }

        public ITMPCommand GetEffect(string name)
        {
            if (database != null && database.ContainsEffect(name)) return database.GetEffect(name);
            if (sceneDatabase != null && sceneDatabase.ContainsKey(name)) return sceneDatabase[name];
            throw new KeyNotFoundException();
        }
    }

    internal class CachedEvent : ITagWrapper, ICachedInvokable
    {
        public EffectTag Tag => args.Tag;
        public EffectTagIndices Indices => args.Indices;

        public TMPEventArgs args { get; private set; }
        public bool Triggered { get; private set; }

        public bool ExecuteInstantly => false;
        public bool ExecuteOnSkip => true;
        public bool ExecuteRepeatable => true;
        public bool ExecuteInPreview => true;

        private TMPEvent tmpEvent;

        public void Trigger()
        {
            if (Triggered) return;

            Triggered = true;
            tmpEvent.Invoke(args);
        }

        public void Reset()
        {
            Triggered = false;
        }

        public CachedEvent(TMPEventArgs args, TMPEvent tmpEvent) => Reset(args, tmpEvent);
        public void Reset(TMPEventArgs args, TMPEvent tmpEvent)
        {
            this.tmpEvent = tmpEvent;
            this.args = args;
            this.Triggered = false;
        }
    }

    internal class EventCacher : ITagCacher<CachedEvent>
    {
        private TMPEvent tmpEvent;
        //private IList<CharData> charData;

        public EventCacher(/*IList<CharData> charData,*/ TMPEvent tmpEvent)
        {
            //this.charData = charData;
            this.tmpEvent = tmpEvent;
        }

        public CachedEvent CacheTag(EffectTag tag, EffectTagIndices indices)
        {
            int endIndex = indices.StartIndex + 1;
            CachedEvent ce = new CachedEvent(new TMPEventArgs(tag, new EffectTagIndices(indices.StartIndex, endIndex, indices.OrderAtIndex)), tmpEvent);
            return ce;
        }
    }

    internal class CommandCacher : ITagCacher<CachedCommand>
    {
        private ITMPEffectDatabase<ITMPCommand> database;
        private TMPWriter writer;
        private IList<CharData> charData;

        public CommandCacher(IList<CharData> charData, TMPWriter writer, ITMPEffectDatabase<ITMPCommand> database)
        {
            this.charData = charData;
            this.writer = writer;
            this.database = database;
        }

        public CachedCommand CacheTag(EffectTag tag, EffectTagIndices indices)
        {
            ITMPCommand command = database.GetEffect(tag.Name);
            int endIndex = indices.EndIndex;

            switch (command.TagType)
            {
                case TagType.Empty: endIndex = indices.StartIndex + 1; break;
                case TagType.Either:
                case TagType.Container: if (indices.IsOpen) endIndex = charData.Count; break;
                default: throw new ArgumentException(nameof(command.TagType));
            }

            EffectTagIndices fixedIndices = new EffectTagIndices(indices.StartIndex, endIndex, indices.OrderAtIndex);
            TMPCommandArgs args = new TMPCommandArgs(tag, fixedIndices, writer);
            CachedCommand cc = new CachedCommand(args, command);
            return cc;
        }
    }

    internal class CachedCommand : ITagWrapper, ICachedInvokable
    {
        public EffectTag Tag => args.tag;
        public EffectTagIndices Indices => args.indices;

        public ITMPCommand command { get; private set; }
        public TMPCommandArgs args { get; private set; }
        public bool Triggered { get; private set; }

        public bool ExecuteInstantly => command.ExecuteInstantly;
        public bool ExecuteOnSkip => command.ExecuteOnSkip;
        public bool ExecuteRepeatable => command.ExecuteRepeatable;
#if UNITY_EDITOR
        public bool ExecuteInPreview => command.ExecuteInPreview;
#endif
        public void Trigger()
        {
            if (Triggered) return;

            Triggered = true;
            command.ExecuteCommand(args);
        }

        public void Reset()
        {
            if (!ExecuteRepeatable) return;
            Triggered = false;
        }

        public CachedCommand(TMPCommandArgs args, ITMPCommand command) => Reset(args, command);
        public void Reset(TMPCommandArgs args, ITMPCommand command)
        {
            this.args = args;
            this.command = command;
            this.Triggered = false;
        }
    }

    internal interface ICachedInvokable : ITagWrapper
    {
        public bool Triggered { get; }
        public bool ExecuteInstantly { get; }
        public bool ExecuteOnSkip { get; }
        public bool ExecuteRepeatable { get; }
#if UNITY_EDITOR
        public bool ExecuteInPreview { get; }
#endif
        public void Reset();
        public void Trigger();
    }

    internal class SceneCommandDatabase : ITMPEffectDatabase<ITMPCommand>
    {
        private IDictionary<string, SceneCommand> dict;

        public SceneCommandDatabase(IDictionary<string, SceneCommand> dict)
        {
            this.dict = dict;
        }

        public bool ContainsEffect(string name)
        {
            return dict.ContainsKey(name);
        }

        public ITMPCommand GetEffect(string name)
        {
            return dict[name];
        }
    }
}