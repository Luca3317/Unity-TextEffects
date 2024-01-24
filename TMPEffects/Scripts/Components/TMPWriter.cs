using AYellowpaper.SerializedCollections;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using TMPEffects.Databases;
using TMPEffects.TextProcessing;
using TMPEffects.TextProcessing.TagProcessors;
using TMPEffects.Tags;
using TMPEffects.Commands;
using System;
using TMPEffects.Extensions;
using System.Linq;

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
        /// <summary>
        /// Is the writer currently writing text?
        /// </summary>
        public bool IsWriting => writing;
        public bool MaySkip => currentMaySkip;
        public int CurrentIndex => currentIndex;

        /// <summary>
        /// The database used to process the text's command tags.
        /// </summary>
        public TMPCommandDatabase CommandDatabase => database;

        public IEnumerable<TMPCommandTag> CommandTags
        {
            get
            {
                foreach (var tag in ctps.ProcessedTags) yield return tag;
            }
        }
        public IEnumerable<TMPEventTag> EventTags
        {
            get
            {
                foreach (var tag in etp.ProcessedTags) yield return tag;
            }
        }

        public int TagCount => ctps.ProcessedTags.Count + etp.ProcessedTags.Count;
        public int CommandTagCount => ctps.ProcessedTags.Count;
        public int EventTagCount => etp.ProcessedTags.Count;

        // Events
        /// <summary>
        /// Raised when the TMPWriter shows the character with index corresponding to the event tag.
        /// </summary>
        public TMPEvent OnTextEvent;
        /// <summary>
        /// Raised when a new character is shown.
        /// </summary>
        public UnityEvent<CharData> OnShowCharacter;
        /// <summary>
        /// Raised when the writer begins writing.
        /// </summary>
        public UnityEvent OnStartWriter;
        /// <summary>
        /// Raised when the writer stops writing.
        /// </summary>
        public UnityEvent OnStopWriter;
        /// <summary>
        /// Raised when the writer is done writing.
        /// </summary>
        public UnityEvent OnFinishWriter;
        /// <summary>
        /// Raised when the writer is skipped until the end of the text.
        /// </summary>
        public UnityEvent OnSkipWriter;
        /// <summary>
        /// Raised when the writer is reset.
        /// </summary>
        public UnityEvent<int> OnResetWriter;

        #region Fields
        [SerializeField] TMPCommandDatabase database;

        [Tooltip("The speed at which the writer shows new characters.")]
        [SerializeField] float speed = 1;

        [Tooltip("Whether the text may be skipped by default.")]
        [SerializeField] bool maySkip = false;

        [Tooltip("If checked, the writer will begin writing when it is first enabled. If not checked, you will have to manually start the writer from your own code.")]
        [SerializeField] bool writeOnStart = true;
        [Tooltip("If checked, the writer will automatically begin writing when the text on the associated TMP_Text component is modified.")]
        [SerializeField] bool autoWriteNewText = true;

        [SerializedDictionary("Tag Name", "Command")]
        [SerializeField] SerializedDictionary<string, SceneCommand> sceneCommands;

        [Tooltip("The speed at which the writer iterates over whitespace characters, as percentage of the general speed")]
        [SerializeField, Range(0, 1)] float whiteSpaceSpeed;
        [Tooltip("The speed at which the writer shows punctuation characters, as percentage of the general speed")]
        [SerializeField, Range(0, 1)] float punctuationSpeed;
        [Tooltip("The speed at which the writer iterates over already visible characters, as percentage of the general speed")]
        [SerializeField, Range(0, 1)] float visibleSpeed;

        [System.NonSerialized] private CommandTagProcessor ctp;
        [System.NonSerialized] private SceneCommandTagProcessor sctp;
        [System.NonSerialized] private EventTagProcessor etp;
        [System.NonSerialized] private TagProcessorStack<TMPCommandTag> ctps;

        [System.NonSerialized] private Coroutine writerCoroutine = null;
        [System.NonSerialized] private float currentSpeed;
        [System.NonSerialized] private bool currentMaySkip;

        [System.NonSerialized] private bool shouldWait = false;
        [System.NonSerialized] private float waitAmount = 0f;
        [System.NonSerialized] Func<bool> continueConditions;

        [System.NonSerialized] private bool writing = false;
        [System.NonSerialized] private int currentIndex = -1;
        #endregion

        #region Initialization
        private void OnEnable()
        {
            UpdateMediator();
            UpdateProcessor(forceReprocess: false);

            etp = new EventTagProcessor(); // Not dependent on any database
            mediator.Processor.RegisterProcessor(ParsingUtility.EVENT_PREFIX, etp);

            mediator.Subscribe(this);
            mediator.TextChanged -= OnTextChanged;
            mediator.TextChanged += OnTextChanged;
            mediator.Processor.FinishAdjustIndeces += PostProcessTags;

            mediator.ForceReprocess();

#if UNITY_EDITOR
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
        #endregion

        #region CleanUp
        private void OnDisable()
        {
            mediator.TextChanged -= OnTextChanged;
            mediator.Processor.FinishAdjustIndeces -= PostProcessTags;
            mediator.Processor.UnregisterProcessor(ParsingUtility.COMMAND_PREFIX);
            mediator.Processor.UnregisterProcessor(ParsingUtility.EVENT_PREFIX);

            // TODO Each reassigned in OnEnable anyway;
            // Either change class to reuse instances or dont reset (atm, resetting is necessary for some editor functionality though)
            etp.Reset();
            ctps.Reset();

            ResetWriter();

            mediator.ForceReprocess();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorApplication.update -= EditorUpdate;
                StopWriterCoroutine();
                currentIndex = -1;
                Show(0, mediator.Text.textInfo.characterCount, true);
                writing = false;
            }
#endif
            if (mediator != null) mediator.Unsubscribe(this);
        }

        private void OnDestroy()
        {
        }
        #endregion

        #region Writer Controlling
        /// <summary>
        /// Start (or resume) writing.
        /// </summary>
        public void StartWriter()
        {
            if (!enabled || !gameObject.activeInHierarchy) return;

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
            if (!enabled || !gameObject.activeInHierarchy) return;

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
            if (!enabled || !gameObject.activeInHierarchy) return;

            if (writing)
            {
                StopWriterCoroutine();
            }

            // reset
            currentIndex = -1;
            Hide(0, mediator.Text.textInfo.characterCount, true);

            OnResetWriter?.Invoke(0);
        }

        /// <summary>
        /// Reset the writer to the given index.
        /// </summary>
        /// <param name="index">The index to reset the writer to.</param>
        public void ResetWriter(int index)
        {
            if (!enabled || !gameObject.activeInHierarchy) return;

            if (writing)
            {
                StopWriterCoroutine();
            }

            // reset
            currentIndex = index;
            Hide(index, mediator.Text.textInfo.characterCount - index, true);
            Show(0, index, true);

            OnResetWriter?.Invoke(index);
        }

        /// <summary>
        /// Finish the writing process. 
        /// This will instantly show all characters, raise all not-yet raised events and execute commands that are tagged with Raise-On-Skip. TODO Implement as defiend here
        /// </summary>
        public void FinishWriter()
        {
            if (!enabled || !gameObject.activeInHierarchy) return;
            if (!currentMaySkip) return;

            int skipTo = mediator.CharData.Count;
            int cachedIndex = ctps.ProcessedTags.FindIndex((x) => x.startIndex >= currentIndex);
            if (cachedIndex >= 0)
            {
                int len = ctps.ProcessedTags.Count;
                TMPCommandTag tag;
                for (; cachedIndex < len; cachedIndex++)
                {
                    tag = ctps.ProcessedTags[cachedIndex];
                    if (tag.name == "skippable")
                    {
                        if (tag.parameters != null && tag.parameters[""] == "false")
                        {
                            skipTo = tag.startIndex;
                            break;
                        }
                    }
                }
            }

            OnSkipWriter?.Invoke();

            currentIndex = skipTo;
            Show(0, skipTo, true);

            // Execute remaining commands / events
            ExecuteCommands(skipTo, true);
            ExecuteEvents(skipTo, true);

            if (skipTo == mediator.CharData.Count)
            {
                if (writing) StopWriterCoroutine();
                OnFinishWriter?.Invoke();
            }

            //if (!enabled || !gameObject.activeInHierarchy) return;
            //if (!maySkip) return; 

            //// skip to end
            //if (writing)
            //{
            //    StopWriterCoroutine();
            //    OnSkipWriter?.Invoke();
            //}

            //// Execute remaining commands / events
            //ExecuteCommands(-1, true);
            //ExecuteEvents(-1, true);

            //currentIndex = mediator.Text.textInfo.characterCount;
            //Show(0, mediator.Text.textInfo.characterCount, true);

            //OnFinishWriter?.Invoke();
        }

        /// <summary>
        /// Restart the writer.
        /// This will reset the writer and start the writing process.
        /// </summary>
        public void RestartWriter()
        {
            if (!enabled) return;

            ResetWriter();
            StartWriter();
        }
        #endregion

        #region Tag Manipulation & various

        // Manipulate based on index in collection
        public TMPCommandTag CommandTagAt(int index)
        {
            return ctps.ProcessedTags[index];
        }
        public TMPEventTag EventTagAt(int index)
        {
            return etp.ProcessedTags[index];
        }

        public int IndexOfCommandTag(TMPCommandTag tag)
        {
            return ctps.ProcessedTags.IndexOf(tag);
        }
        public int IndexOfEventTag(TMPEventTag tag)
        {
            return etp.ProcessedTags.IndexOf(tag);
        }

        public void RemoveCommandTagAt(int index)
        {
            ctps.ProcessedTags.RemoveAt(index);
        }
        public void RemoveEventTagAt(int index)
        {
            etp.ProcessedTags.RemoveAt(index);
        }

        // Manipulate based on index in the text
        public void CommandTagAtTextIndex(int textIndex, ICollection<TMPCommandTag> tags)
        {
            tags.AddRange(ctps.ProcessedTags.Where(x => x.startIndex == textIndex));
        }
        public void EventTagAtTextIndex(int textIndex, ICollection<TMPEventTag> tags)
        {
            tags.AddRange(etp.ProcessedTags.Where(x => x.startIndex == textIndex));
        }

        public bool TryInsertCommandTag(string tag, int textIndex = 0, int length = -1)
        {
            ParsingUtility.TagInfo tagInfo = new ParsingUtility.TagInfo();
            bool parsed = ParsingUtility.TryParseTag(tag, 0, tag.Length - 1, ref tagInfo, ParsingUtility.TagType.Open);
            if (!parsed) return false;

            Dictionary<string, string> parameters;
            if (!ValidateCommandTag(tag, tagInfo, out parameters)) return false;

            TMPCommandTag t = new TMPCommandTag(tagInfo.name, textIndex, 0, parameters);

            if (length < 0) t.Close(mediator.CharData.Count - 1);
            else if (length == 0) t.Close(textIndex);
            else t.Close(textIndex + length - 1);

            InsertElement(ctps.ProcessedTags, t);
            cachedCommands.Add(t, new CachedCommand(database.GetEffect(t.name), new TMPCommandArgs(t, this)));

            return true;
        }
        public bool TryInsertCommandTag(string key, Dictionary<string, string> parameters, int textIndex = 0, int length = -1)
        {
            if (textIndex < 0 || textIndex >= mediator.CharData.Count || (textIndex + length) > mediator.CharData.Count) throw new System.IndexOutOfRangeException();
            if (!ValidateCommandTag(key, parameters)) return false;

            TMPCommandTag t = new TMPCommandTag(key, textIndex, 0, parameters);

            if (length < 0) t.Close(mediator.CharData.Count - 1);
            else if (length == 0) t.Close(textIndex);
            else t.Close(textIndex + length - 1);

            InsertElement(ctps.ProcessedTags, t);
            cachedCommands.Add(t, new CachedCommand(database.GetEffect(t.name), new TMPCommandArgs(t, this)));

            return true;
        }
        public bool TryInsertEventTag(string tag, int textIndex = 0)
        {
            ParsingUtility.TagInfo tagInfo = new ParsingUtility.TagInfo();
            bool parsed = ParsingUtility.TryParseTag(tag, 0, tag.Length - 1, ref tagInfo, ParsingUtility.TagType.Open);
            if (!parsed) return false;

            Dictionary<string, string> parameters = ParsingUtility.GetTagParametersDict(tag);
            TMPEventTag t = new TMPEventTag(textIndex, tagInfo.name, 0, parameters);

            InsertElement(etp.ProcessedTags, t);
            cachedEvents.Add(t, new CachedEvent(new TMPEventArgs(t)));

            return true;
        }
        public void InsertEventTag(string key, Dictionary<string, string> parameters, int textIndex = 0)
        {
            TMPEventTag t = new TMPEventTag(textIndex, key, 0, parameters);
            InsertElement(etp.ProcessedTags, t);
            cachedEvents.Add(t, new CachedEvent(new TMPEventArgs(t)));
        }

        public void RemoveCommandTagAtTextIndex(int textIndex, int maxRemove = -1, ICollection<TMPCommandTag> coll = null)
        {
            if (textIndex < 0 || textIndex >= mediator.CharData.Count) throw new System.IndexOutOfRangeException();

            if (maxRemove < 0) maxRemove = ctps.ProcessedTags.Count;

            for (int i = 0; i < ctps.ProcessedTags.Count; i++)
            {
                if (maxRemove <= 0) return;
                if (ctps.ProcessedTags[i].startIndex == textIndex)
                {
                    var tag = ctps.ProcessedTags[i];
                    ctps.ProcessedTags.RemoveAt(i);
                    cachedCommands.Remove(tag);
                    coll?.Add(tag);
                    i--;
                }
            }
        }
        public void RemoveEventTagAtTextIndex(int textIndex, int maxRemove = -1, ICollection<TMPEventTag> coll = null)
        {
            if (textIndex < 0 || textIndex >= mediator.CharData.Count) throw new System.IndexOutOfRangeException();

            if (maxRemove < 0) maxRemove = etp.ProcessedTags.Count;

            for (int i = 0; i < etp.ProcessedTags.Count; i++)
            {
                if (maxRemove <= 0) return;
                if (etp.ProcessedTags[i].startIndex == textIndex)
                {
                    var tag = etp.ProcessedTags[i];
                    etp.ProcessedTags.RemoveAt(i);
                    cachedEvents.Remove(tag);
                    coll?.Add(tag);
                    i--;
                }
            }
        }

        public void ClearTags()
        {
            ctps.ProcessedTags.Clear();
            etp.ProcessedTags.Clear();
        }
        public void ClearCommandTags()
        {
            ctps.ProcessedTags.Clear();
        }
        public void ClearEventTags()
        {
            etp.ProcessedTags.Clear();
        }

        private bool ValidateCommandTag(string tag, ParsingUtility.TagInfo tagInfo, out Dictionary<string, string> parametersOut)
        {
            parametersOut = null;
            if (!database.ContainsEffect(tagInfo.name)) return false;
            parametersOut = ParsingUtility.GetTagParametersDict(tag);
            if (!database.GetEffect(tagInfo.name).ValidateParameters(parametersOut)) return false;
            return true;
        }
        private bool ValidateCommandTag(string key, Dictionary<string, string> parameters)
        {
            if (!database.ContainsEffect(key)) return false;
            if (!database.GetEffect(key).ValidateParameters(parameters)) return false;
            return true;
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
            currentSpeed = speed;
        }

        /// <summary>
        /// Set whether the current text may be skipped.
        /// </summary>
        /// <param name="skippable"></param>
        public void SetSkippable(bool skippable)
        {
            currentMaySkip = skippable;
        }
        #endregion

        #region Setters
        /// <summary>
        /// Set the database that will be used to parse command tags.
        /// </summary>
        /// <param name="database">The database that will be used to parse command tags.</param>
        public void SetDatabase(TMPCommandDatabase database)
        {
            this.database = database;
            UpdateProcessor();
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
                else Show(0, mediator.CharData.Count, true);
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



        private IEnumerator Execute(int index)
        {
            int cmdIndex = ctps.ProcessedTags.FindIndex(x => x.startIndex == index);
            int evtIndex = etp.ProcessedTags.FindIndex(x => x.startIndex == index);
            int cmdlen = ctps.ProcessedTags.Count;
            int evtlen = etp.ProcessedTags.Count;
            cmdIndex = cmdIndex == -1 ? cmdlen : cmdIndex;
            evtIndex = evtIndex == -1 ? evtlen : evtIndex;
            TMPCommandTag cmd = cmdIndex == cmdlen ? null : ctps.ProcessedTags[cmdIndex];
            TMPEventTag evt = evtIndex == evtlen ? null : etp.ProcessedTags[evtIndex];

#if UNITY_EDITOR
            // When testing in edit mode
            if (!Application.isPlaying)
            {
                if (!commandsEnabled) cmd = null;
                if (!eventsEnabled) evt = null;
            }
#endif

            while (cmd != null && evt != null)
            {
                if (cmd.orderAtIndex < evt.orderAtIndex)
                {
                    cachedCommands[cmd].Trigger();
                    cmdIndex++;
                    cmd = cmdIndex == cmdlen ? null : ctps.ProcessedTags[cmdIndex];
                    if (cmd?.startIndex > index) cmd = null;
                }
                else
                {
                    cachedEvents[evt].Trigger(OnTextEvent);
                    evtIndex++;
                    evt = evtIndex == evtlen ? null : etp.ProcessedTags[evtIndex];
                    if (evt?.startIndex > index) evt = null;
                }

                if (shouldWait) yield return new WaitForSeconds(waitAmount);
                waitAmount = 0f;

                yield return HandleWaitConditions();
            }

            while (cmd != null)
            {
                cachedCommands[cmd].Trigger();
                cmdIndex++;
                cmd = cmdIndex == cmdlen ? null : ctps.ProcessedTags[cmdIndex];
                if (cmd?.startIndex > index) cmd = null;

                if (shouldWait) yield return new WaitForSeconds(waitAmount);
                waitAmount = 0f;

                yield return HandleWaitConditions();
            }

            while (evt != null)
            {
                cachedEvents[evt].Trigger(OnTextEvent);
                evtIndex++;
                evt = evtIndex == evtlen ? null : etp.ProcessedTags[evtIndex];
                if (evt?.startIndex > index) evt = null;

                if (shouldWait) yield return new WaitForSeconds(waitAmount);
                waitAmount = 0f;

                yield return HandleWaitConditions();
            }
        }

        IEnumerator WriterCoroutine()
        {
            OnStartWriting();

            // TODO This indicates the text is fully shown already
            if (currentIndex >= mediator.Text.textInfo.characterCount)
            {
                OnStopWriting();
                yield break;
            }

            // Reset all relevant variables
            currentSpeed = speed;
            currentMaySkip = maySkip;

            TMP_TextInfo info = mediator.Text.textInfo;
            CharData cData;

            if (currentIndex == -1)
                HideAllCharacters(true);

            // Execute instant commands
            ExecuteCommands(-1);

            for (int i = Mathf.Max(currentIndex, 0); i < info.characterCount; i++)
            {
                currentIndex = i;
                cData = mediator.CharData[i];

                // Execute the commands & events for the current index
                yield return Execute(i);

                OnShowCharacter?.Invoke(cData);

                // TODO Toggle for whether invisible character still take time to show
                if (!cData.info.isVisible)
                {
                    if (speed > 0 && whiteSpaceSpeed > 0) yield return new WaitForSeconds((1f / (currentSpeed / whiteSpaceSpeed)) * 0.1f);
                    continue;
                }
                // TODO Toggle for whether already visible characters still take time to show
                if (cData.visibilityState == CharData.VisibilityState.Shown)
                {
                    if (speed > 0 && visibleSpeed > 0) yield return new WaitForSeconds((1f / (currentSpeed / visibleSpeed)) * 0.1f);
                    continue;
                }

                Show(i, 1, false);
                mediator.ForceUpdate(i, 1);

                if (currentSpeed > 0)
                {
                    if (Char.IsPunctuation(cData.info.character))
                    {
                        if (punctuationSpeed > 0)
                            yield return new WaitForSeconds((1f / (currentSpeed / punctuationSpeed)) * 0.1f);
                    }
                    else yield return new WaitForSeconds((1f / currentSpeed) * 0.1f);
                }
            }

            OnFinishWriter?.Invoke();
            OnStopWriting();

            //Hide(0, mediator.Text.textInfo.characterCount, false);
        }

        // TODO Toggle for whether to wait 
        //      Until all conditions have been true at one point since starting the check
        //      Until all conditions are true at once
        IEnumerator HandleWaitConditions()
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

        private void ExecuteEvents(int index, bool skipped = false)
        {
#if UNITY_EDITOR
            if (!eventsEnabled && !Application.isPlaying) return;
#endif

            TMPEventTag eventTag;

            if (skipped)
            {
                foreach (var cachedEvent in cachedEvents)
                {
                    if (cachedEvent.Key.startIndex >= index) break;
                    if (cachedEvent.Key.invokeOnSkip)
                        cachedEvent.Value.Trigger(OnTextEvent);
                }

                return;
            }

            int cachedIndex = etp.ProcessedTags.FindIndex((x) => x.startIndex == index);
            int len = etp.ProcessedTags.Count;
            if (cachedIndex >= 0)
            {
                while (cachedIndex < len && (eventTag = etp.ProcessedTags[cachedIndex++]).startIndex == index)
                {
                    cachedEvents[eventTag].Trigger(OnTextEvent);
                }
            }
        }

        private void ExecuteCommands(int index, bool skipped = false)
        {
#if UNITY_EDITOR
            if (!commandsEnabled && !Application.isPlaying) return;
#endif
            TMPCommandTag commandTag;

            // If writer skipped, execute remaining commands
            if (skipped)
            {
                foreach (var cachedCommand in cachedCommands)
                {
                    if (cachedCommand.Key.startIndex >= index) break;
                    if (cachedCommand.Value.command.ExecuteOnSkip)
                        cachedCommand.Value.Trigger();
                }

                return;
            }

            // If index negative, execute executeinstantly commands
            if (index < 0)
            {
                foreach (var cachedCommand in cachedCommands.Values)
                {
                    if (cachedCommand.command.ExecuteInstantly)
                        cachedCommand.Trigger();
                }
                return;
            }

            // Otherwise, execute commands at current index
            int cachedIndex = ctps.ProcessedTags.FindIndex((x) => x.startIndex == index);
            int len = ctps.ProcessedTags.Count;
            if (cachedIndex >= 0)
            {
                while (cachedIndex < len && (commandTag = ctps.ProcessedTags[cachedIndex++]).startIndex == index)
                {
                    cachedCommands[commandTag].Trigger();
                }
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
                mediator.ForceReprocess();
                reprocessFlag = false;
            }
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private void OnValidate()
        {
            // Ensure data is set - OnValidate often called before OnEnable
            if (mediator == null || !mediator.isInitialized) return;

            if (database != prevDatabase)
            {
                prevDatabase = database;
                UpdateProcessor();

                reprocessFlag = true;
            }
        }

        public void ForceReprocess()
        {
            mediator.ForceReprocess();
        }
#endif
        #endregion

        private void HideAllCharacters(bool skipAnimations = false)
        {
            Hide(0, mediator.Text.textInfo.characterCount, skipAnimations);
        }

        private void UpdateProcessor(bool forceReprocess = true)
        {
            mediator.Processor.UnregisterProcessor(ParsingUtility.COMMAND_PREFIX);
            ctps = new TagProcessorStack<TMPCommandTag>();
            ctps.AddProcessor(ctp = new CommandTagProcessor(database));
            ctps.AddProcessor(sctp = new SceneCommandTagProcessor(sceneCommands));
            mediator.Processor.RegisterProcessor(ParsingUtility.COMMAND_PREFIX, ctps);

            if (forceReprocess) mediator.ForceReprocess();
        }


        Dictionary<TMPCommandTag, CachedCommand> cachedCommands = new Dictionary<TMPCommandTag, CachedCommand>();
        Dictionary<TMPEventTag, CachedEvent> cachedEvents = new Dictionary<TMPEventTag, CachedEvent>();

        private void PostProcessTags(string text)
        {
            int endIndex = text.Length - 1;
            foreach (var tag in ctps.ProcessedTags)
            {
                if (tag.IsOpen)
                    tag.Close(endIndex);
            }

            // Sort the processed commands by startIndex; this is necessary since this
            // wont be autosorted, as it combines two unrelated lists (those are autosorted each)
            ctps.ProcessedTags.Sort((x, y) => x.startIndex < y.startIndex ? -1 : 1);

            cachedCommands.Clear();
            cachedEvents.Clear();
            foreach (var commandTag in ctp.ProcessedTags) cachedCommands.Add(commandTag, new CachedCommand(database.GetEffect(commandTag.name), new TMPCommandArgs(commandTag, this)));
            foreach (var sceneCommandTag in sctp.ProcessedTags) cachedCommands.Add(sceneCommandTag, new CachedCommand(sceneCommands[sceneCommandTag.name], new TMPCommandArgs(sceneCommandTag, this)));
            foreach (var eventTag in etp.ProcessedTags) cachedEvents.Add(eventTag, new CachedEvent(new TMPEventArgs(eventTag)));
        }

        /// <summary>
        /// Show the specified characters.
        /// </summary>
        /// <param name="start">The startindex of the characters to be shown.</param>
        /// <param name="length">The amount of characters to be shown, starting from <paramref name="start"/></param>
        /// <exception cref="System.ArgumentException">Throws if either <paramref name="start"/> or <paramref name="length"/> is out of bounds.</exception>
        public void Show(int start, int length, bool skipAnimation = false)
        {
            TMP_TextInfo info = mediator.Text.textInfo;
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
                cData = mediator.CharData[i];
                if (!cData.info.isVisible) continue;

                // Set the current mesh's vertices all to the initial mesh values
                for (int j = 0; j < 4; j++)
                {
                    cData.mesh.SetPosition(j, cData.mesh.initial.GetPosition(j));
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
                    colors[vIndex + j] = cData.mesh[j].color;
                }

                // Apply the new vertices to the char data array
                mediator.CharData[i] = cData;
                mediator.VisibilityStateUpdated(i, prev);
            }

            if (mediator.Text.mesh != null)
                mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        /// <summary>
        /// Hide the specified characters.
        /// </summary>
        /// <param name="start">The startindex of the characters to be hidden.</param>
        /// <param name="length">The amount of characters to be hidden, starting from <paramref name="start"/></param>
        /// <exception cref="System.ArgumentException">Throws if either <paramref name="start"/> or <paramref name="length"/> is out of bounds.</exception>
        public void Hide(int start, int length, bool skipAnimation = false)
        {
            TMP_TextInfo info = mediator.Text.textInfo;
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

                cData = mediator.CharData[i];

                // Set the current mesh's vertices all to the initial mesh values
                for (int j = 0; j < 4; j++)
                {
                    cData.mesh.SetPosition(j, cData.info.initialPosition);
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
                    colors[vIndex + j] = cData.mesh[j].color;
                }

                // Apply the new vertices to the char data array
                mediator.CharData[i] = cData;
                mediator.VisibilityStateUpdated(i, prev);
            }

            if (mediator.Text.mesh != null)
                mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);

            mediator.ForceUpdate(start, length);
        }



        private class CachedCommand
        {
            public ITMPCommand command { get; private set; }
            public TMPCommandArgs args { get; private set; }
            public bool Triggered { get; private set; }

            public void Trigger()
            {
                if (Triggered) return;
                //Debug.Log("Trigger command " + args.tag.name + " at " + args.tag.startIndex);
                Triggered = true;
                command.ExecuteCommand(args);
            }

            public CachedCommand(ITMPCommand command, TMPCommandArgs args) => Reset(command, args);
            public void Reset(ITMPCommand command, TMPCommandArgs args)
            {
                this.command = command;
                this.args = args;
                this.Triggered = false;
            }
        }

        private class CachedEvent
        {
            public TMPEventArgs args { get; private set; }
            public bool Triggered { get; private set; }

            public void Trigger(TMPEvent tmpEvent)
            {
                if (Triggered) return;
                //Debug.Log("Trigger event " + args.tag.name + " at " + args.tag.startIndex);
                Triggered = true;
                tmpEvent.Invoke(args);
            }

            public CachedEvent(TMPEventArgs args) => Reset(args);
            public void Reset(TMPEventArgs args)
            {
                this.args = args;
                Triggered = false;
            }
        }
    }
}