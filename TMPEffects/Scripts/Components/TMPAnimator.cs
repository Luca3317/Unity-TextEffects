using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using TMPEffects.Animations;
using TMPEffects.Databases;
using TMPEffects.Tags;
using TMPEffects.TextProcessing;
using TMPEffects.TextProcessing.TagProcessors;
using System.Linq;
using System;
using TMPEffects.Extensions;
using PlasticGui.Gluon.WorkspaceWindow;
using System.Xml.Serialization;
using Palmmedia.ReportGenerator.Core.Parser.Analysis;
using Unity.VisualScripting.YamlDotNet.Core.Tokens;

namespace TMPEffects.Components
{
    /// <summary>
    /// Animates the character of a <see cref="TMP_Text"/> component.
    /// </summary>
    /// <remarks>
    /// One of the two main components of TMPEffects, along with <see cref="TMPWriter"/>.<br/>
    /// TMPAnimator allows you to apply animations to the characters of a <see cref="TMP_Text"/> component.<br/>
    /// There are three types of animations:
    /// <list type="table">
    /// <item><see cref="TMPAnimation"/>: The "basic" type of animation. Will animate the effected text continuously.</item>
    /// <item><see cref="TMPShowAnimation"/>: Will animate the effected text when it begins to be shown. Show animations are only applied if there is also a <see cref="TMPWriter"/> component on the same GameObject.</item>
    /// <item><see cref="TMPHideAnimation"/>: Will animate the effected text when it begins to be hidden. Hide animations are only applied if there is also a <see cref="TMPWriter"/> component on the same GameObject.</item>
    /// </list>
    /// </remarks>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(TMP_Text))]
    public class TMPAnimator : TMPEffectComponent
    {
        /// <summary>
        /// Is the text currently being animated?
        /// </summary>
        public bool IsAnimating => updateFrom == UpdateFrom.Script || isAnimating;
        /// <summary>
        /// The database used to process the text's animation tags.
        /// </summary>
        public TMPAnimationDatabase Database => database;

        public UpdateFrom UpdateFrom => updateFrom;

        /// <summary>
        /// The animation tags parsed by the TMPAnimator.
        /// </summary>
        public IEnumerable<TMPAnimationTag> Tags
        {
            get
            {
                for (int i = 0; i < atp.ProcessedTags.Count; i++) yield return atp.ProcessedTags[i];
                for (int i = 0; i < satp.ProcessedTags.Count; i++) yield return satp.ProcessedTags[i];
                for (int i = 0; i < hatp.ProcessedTags.Count; i++) yield return hatp.ProcessedTags[i];
            }
        }
        /// <summary>
        /// The basic animation tags parsed by the TMPAnimator.
        /// </summary>
        public IEnumerable<TMPAnimationTag> BasicTags
        {
            get
            {
                for (int i = 0; i < atp.ProcessedTags.Count; i++) yield return atp.ProcessedTags[i];
            }
        }
        /// <summary>
        /// The show animation tags parsed by the TMPAnimator.
        /// </summary>
        public IEnumerable<TMPAnimationTag> ShowTags
        {
            get
            {
                for (int i = 0; i < satp.ProcessedTags.Count; i++) yield return satp.ProcessedTags[i];
            }
        }
        /// <summary>
        /// The hide animation tags parsed by the TMPAnimator.
        /// </summary>
        public IEnumerable<TMPAnimationTag> HideTags
        {
            get
            {
                for (int i = 0; i < hatp.ProcessedTags.Count; i++) yield return hatp.ProcessedTags[i];
            }
        }

        /// <summary>
        /// The amount of tags parsed by the TMPAnimator.
        /// </summary>
        public int TagCount => atp.ProcessedTags.Count + satp.ProcessedTags.Count + hatp.ProcessedTags.Count;
        /// <summary>
        /// The amount of basic tags parsed by the TMPAnimator.
        /// </summary>
        public int BasicTagCount => atp.ProcessedTags.Count;
        /// <summary>
        /// The amount of show tags parsed by the TMPAnimator.
        /// </summary>
        public int ShowTagCount => satp.ProcessedTags.Count;
        /// <summary>
        /// The amount of hide parsed by the TMPAnimator.
        /// </summary>
        public int HideTagCount => hatp.ProcessedTags.Count;

        #region Fields
        [SerializeField] TMPAnimationDatabase database;
        [SerializeField] AnimatorContext context;

        [SerializeField] UpdateFrom updateFrom;
        [SerializeField] bool animateOnStart = true;

        [SerializeField] bool animationsOverride = false;
        [SerializeField] string defaultShowString;
        [SerializeField] string defaultHideString;

        [SerializeField] string excludedCharacters = "";
        [SerializeField] string excludedCharactersShow = "";
        [SerializeField] string excludedCharactersHide = "";

        [SerializeField] bool excludePunctuation = false;
        [SerializeField] bool excludePunctuationShow = false;
        [SerializeField] bool excludePunctuationHide = false;

        [System.NonSerialized] private AnimationTagProcessor<TMPAnimation> atp = null;
        [System.NonSerialized] private AnimationTagProcessor<TMPShowAnimation> satp = null;
        [System.NonSerialized] private AnimationTagProcessor<TMPHideAnimation> hatp = null;
        [System.NonSerialized] private bool isAnimating = false;

        [System.NonSerialized] private CachedAnimation dummyShow;
        [System.NonSerialized] private CachedAnimation dummyHide;
        [System.NonSerialized] private CachedAnimation defaultShow;
        [System.NonSerialized] private CachedAnimation defaultHide;
        #endregion 

        #region Initialization
        private void OnEnable()
        {
            // Set up the mediator and processor, and subscribe to relevant events

            UpdateMediator(); // Create / get the mediator; Initialize it if not initialized already
            UpdateProcessors(); // Set up the animation tag processor and add it to the mediator

            mediator.OnVisibilityStateUpdated += EnsureCorrectTiming;

            mediator.Subscribe(this); // Subscribe to the mediator; This makes the mediator persistent at least until this component is destroyed

            mediator.CharDataPopulated += PostProcessTags; // Close still open tags once tag processing is done

            mediator.TextChanged += OnTextChanged; // Subscribe to the relevant events
            mediator.ForcedUpdate += OnForcedUpdate;

            mediator.ForceReprocess(); // Force a reprocess of the text now that processor is added and mediator events are handled

#if UNITY_EDITOR
            if (preview && !Application.isPlaying) StartPreview();
#endif
        }

        private void Start()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif

            if (animateOnStart && updateFrom != UpdateFrom.Script) StartAnimating();
        }
        #endregion

        #region CleanUp
        private void OnDisable()
        {
            // Unsubscribe from all events
            mediator.OnVisibilityStateUpdated -= EnsureCorrectTiming;
            mediator.CharDataPopulated -= PostProcessTags;
            mediator.TextChanged -= OnTextChanged;
            mediator.ForcedUpdate -= OnForcedUpdate;

            mediator.Processor.UnregisterProcessor(ParsingUtility.NO_PREFIX); // Remove animation tag processor from mediator
            mediator.Processor.UnregisterProcessor(ParsingUtility.SHOW_ANIMATION_PREFIX);
            mediator.Processor.UnregisterProcessor(ParsingUtility.HIDE_ANIMATION_PREFIX);
            atp.Reset(); // Reset animation tag processor itself

            mediator.ForceReprocess(); // Force a reprocess of the text

#if UNITY_EDITOR
            StopPreview();
            //if (!Application.isPlaying) EditorApplication.delayCall += StopPreview;
#endif
            if (mediator != null) mediator.Unsubscribe(this); // Unsubscribe from the mediator; if this was the last subscriber, mediator will be destroyed
        }

        private void OnDestroy()
        {
        }
        #endregion

        #region Animation Controlling
        /// <summary>
        /// Update the current animations.
        /// </summary>
        /// TODO Enforce calling StartAnimating when UpdateFrom.Script?
        /// TODO Allow calling this when not updating from Script?
        public void UpdateAnimations(float deltaTime)
        {
#if UNITY_EDITOR
            if (Application.isPlaying && updateFrom != UpdateFrom.Script)
            {
                Debug.LogWarning(string.Format(falseUpdateAnimationsCallWarning, name, updateFrom.ToString()));
                return;
            }
#else
            if (updateFrom != UpdateFrom.Script)
            {
                Debug.LogWarning(string.Format(falseUpdateAnimationsCallWarning, name, updateFrom.ToString()));
                return;
            }
#endif

            UpdateAnimations_Impl(deltaTime);
        }

        /// <summary>
        /// Start animating.
        /// </summary>
        public void StartAnimating()
        {
#if UNITY_EDITOR
            if (Application.isPlaying && updateFrom == UpdateFrom.Script)
            {
                Debug.LogWarning(string.Format(falseStartStopAnimatingCallWarning, "StartAnimating", name));
                return;
            }
#else
            if (updateFrom == UpdateFrom.Script)
            {
                Debug.LogWarning(string.Format(falseStartStopAnimatingCallWarning, "StartAnimating", name));
                return;
            }
#endif

            isAnimating = true;
        }

        /// <summary>
        /// Stop animating.
        /// </summary>
        public void StopAnimating()
        {
#if UNITY_EDITOR
            if (Application.isPlaying && updateFrom == UpdateFrom.Script)
            {
                Debug.LogWarning(string.Format(falseStartStopAnimatingCallWarning, "StopAnimating", name));
                return;
            }
#else
            if (updateFrom == UpdateFrom.Script)
            {
                Debug.LogWarning(string.Format(falseStartStopAnimatingCallWarning, "StopAnimating", name));
                return;
            }
#endif

            isAnimating = false;
        }

        /// <summary>
        /// Reset all visible characters to their initial state.
        /// </summary>
        public void ResetAnimations() => ResetAllVisible();
        #endregion

        #region Setters
        /// <summary>
        /// Set where the animations should be updated from.
        /// </summary>
        /// <param name="updateFrom"></param>
        public void SetUpdateFrom(UpdateFrom updateFrom)
        {
            if (isAnimating)
            {
                StopAnimating();
            }

            this.updateFrom = updateFrom;
        }

        /// <summary>
        /// Set the database the animator should use to parse the text's animation tags.
        /// </summary>
        /// <param name="database"></param>
        public void SetDatabase(TMPAnimationDatabase database)
        {
            this.database = database;
            UpdateProcessors();
            mediator.ForceReprocess();
        }
        #endregion

        #region Tag Manipulation & various
        // Manipulate based on index in collection
        /// <summary>
        /// Get the animation tag at the given index.<br/>
        /// <paramref name="index"/> refers to the index within <see cref="Tags"/>.
        /// </summary>
        /// <param name="index"></param>
        /// <returns>The animation tag at the given index</returns>
        /// <exception cref="IndexOutOfRangeException">If the index is out of range of <see cref="Tags"/></exception>
        public TMPAnimationTag TagAt(int index)
        {
            if (index < atp.ProcessedTags.Count) return atp.ProcessedTags[index];

            index -= atp.ProcessedTags.Count;
            if (index < satp.ProcessedTags.Count) return satp.ProcessedTags[index];

            index -= satp.ProcessedTags.Count;
            if (index < hatp.ProcessedTags.Count) return hatp.ProcessedTags[index];

            throw new IndexOutOfRangeException();
        }
        public TMPAnimationTag TagAt(int index, TMPAnimationType type)
        {
            switch (type)
            {
                case TMPAnimationType.Basic: return atp.ProcessedTags[index];
                case TMPAnimationType.Show: return satp.ProcessedTags[index];
                case TMPAnimationType.Hide: return hatp.ProcessedTags[index];
                default: return null;
            }
        }
        public TMPAnimationTag BasicTagAt(int index) => atp.ProcessedTags[index];
        public TMPAnimationTag ShowTagAt(int index) => satp.ProcessedTags[index];
        public TMPAnimationTag HideTagAt(int index) => hatp.ProcessedTags[index];

        /// <summary>
        /// Get the index of the given tag.        
        /// If no <paramref name="type"/> is defined, the return value refers to the tag's index within <see cref="Tags"/>. <br/>
        /// Otherwise, it refers to the tag's index within <see cref="BasicTags"/> / <see cref="ShowTags"/> / <see cref="HideTags"/>
        /// </summary>
        /// <param name="tag">The tag of which to get the index</param>
        /// <param name="type">Type of the tag</param>
        /// <returns>The index of the given tag.        
        /// If no <paramref name="type"/> is defined, the return value refers to the tag's index within <see cref="Tags"/>. <br/>
        /// Otherwise, it refers to the tag's index within <see cref="BasicTags"/> / <see cref="ShowTags"/> / <see cref="HideTags"/>
        /// </returns>
        public int IndexOfTag(TMPAnimationTag tag, TMPAnimationType? type = null)
        {
            if (type == null)
            {
                if (atp.ProcessedTags.Contains(tag)) return atp.ProcessedTags.IndexOf(tag);
                if (satp.ProcessedTags.Contains(tag)) return satp.ProcessedTags.IndexOf(tag);
                if (hatp.ProcessedTags.Contains(tag)) return hatp.ProcessedTags.IndexOf(tag);
                return -1;
            }

            switch (type)
            {
                case TMPAnimationType.Basic: return atp.ProcessedTags.IndexOf(tag);
                case TMPAnimationType.Show: return satp.ProcessedTags.IndexOf(tag);
                case TMPAnimationType.Hide: return hatp.ProcessedTags.IndexOf(tag);
            }

            return -1;
        }

        /// <summary>
        /// Remove the tag at the given <paramref name="index"/>.
        /// </summary>
        /// <param name="index">        
        /// The index of the tag.<br/>
        /// If no <paramref name="type"/> is defined, <paramref name="index"/> refers to the tag's index within <see cref="Tags"/>. <br/>
        /// Otherwise, <paramref name="index"/> refers to the tag's index within <see cref="BasicTags"/> / <see cref="ShowTags"/> / <see cref="HideTags"/>
        /// </param>
        /// <param name="type">The type of the tag to remove</param>
        /// <exception cref="IndexOutOfRangeException">If the index is outside of the range of the tag collection</exception>
        public void RemoveTagAt(int index, TMPAnimationType? type = null)
        {
            if (index < 0) throw new IndexOutOfRangeException();
            if (type == null)
            {
                if (index < atp.ProcessedTags.Count)
                {
                    RemoveTag_Impl(TMPAnimationType.Basic, index);
                    return;
                }

                index -= atp.ProcessedTags.Count;
                if (index < satp.ProcessedTags.Count)
                {
                    RemoveTag_Impl(TMPAnimationType.Show, index);
                    return;
                }

                index -= satp.ProcessedTags.Count;
                if (index < hatp.ProcessedTags.Count)
                {
                    RemoveTag_Impl(TMPAnimationType.Hide, index);
                    return;
                }

                throw new IndexOutOfRangeException();
            }

            RemoveTag_Impl(type.Value, index);
        }

        public void TagAtTextIndex(int index, ICollection<TMPAnimationTag> tags)
        {
            if (index < atp.ProcessedTags.Count)
            {
                tags.AddRange(atp.ProcessedTags.Where(x => x.startIndex == index));
                return;
            }

            index -= atp.ProcessedTags.Count;
            if (index < satp.ProcessedTags.Count)
            {
                tags.AddRange(satp.ProcessedTags.Where(x => x.startIndex == index));
                return;
            }

            index -= satp.ProcessedTags.Count;
            if (index < hatp.ProcessedTags.Count)
            {
                tags.AddRange(hatp.ProcessedTags.Where(x => x.startIndex == index));
                return;
            }

            throw new IndexOutOfRangeException();
        }
        public void TagAtTextIndex(int index, TMPAnimationType type, ICollection<TMPAnimationTag> tags)
        {
            switch (type)
            {
                case TMPAnimationType.Basic: tags.AddRange(atp.ProcessedTags.Where(x => x.startIndex == index)); break;
                case TMPAnimationType.Show: tags.AddRange(satp.ProcessedTags.Where(x => x.startIndex == index)); break;
                case TMPAnimationType.Hide: tags.AddRange(hatp.ProcessedTags.Where(x => x.startIndex == index)); break;
            }
        }
        public void BasicTagAtTextIndex(int index, ICollection<TMPAnimationTag> tags) => tags.AddRange(atp.ProcessedTags.Where(x => x.startIndex == index));
        public void ShowTagAtTextIndex(int index, ICollection<TMPAnimationTag> tags) => tags.AddRange(satp.ProcessedTags.Where(x => x.startIndex == index));
        public void HideTagAtTextIndex(int index, ICollection<TMPAnimationTag> tags) => tags.AddRange(hatp.ProcessedTags.Where(x => x.startIndex == index));

        /// <summary>
        /// Insert a tag into the text that is being animated by this animator.
        /// </summary>
        /// <param name="tag">The tag literal. Has to be a well-formed tag, e.g. \<wave amplitude=10\></param>
        /// <param name="textIndex">The index of the tag within the animated text</param>
        /// <param name="length">The "length" of the tag, i.e. how many characters it effects. Negative values will effect all characters after the given <paramref name="textIndex"/></param>
        /// <returns>Whether the insertion was successful (i.e. whether the tag could successfully be validated)</returns>
        /// <exception cref="System.IndexOutOfRangeException">If either the <paramref name="textIndex"/> or <paramref name="length"/> parameter is out of range</exception>
        public bool TryInsertTag(string tag, int textIndex = 0, int length = -1) => InsertTag_Impl(tag, textIndex, length);
        /// <summary>
        /// Insert a tag into the text that is being animated by this animator.
        /// </summary>
        /// <param name="type">The type of the tag, i.e. whether this is a basic / show / hide animation</param>
        /// <param name="key">The key (or name) of the tag</param>
        /// <param name="parameters">The parameters of the tag</param>
        /// <param name="textIndex">The index of the text within the animated text</param>
        /// <param name="length">The "length" of the tag, i.e. how many characters it effects. Negative values will effect all characters after the given <paramref name="textIndex"/></param>
        /// <returns>Whether the insertion was successful (i.e. whether the tag could successfully be validated)</returns>
        /// <exception cref="System.IndexOutOfRangeException">If either the <paramref name="textIndex"/> or <paramref name="length"/> parameter is out of range</exception>
        public bool TryInsertTag(TMPAnimationType type, string key, Dictionary<string, string> parameters, int textIndex = 0, int length = -1)
            => InsertTag_Impl(type, key, parameters, textIndex, length);

        /// <summary>
        /// Remove tags at the given <paramref name="textIndex"/>.
        /// </summary>
        /// <param name="textIndex">The index in the animated text</param>
        /// <param name="type">The type of tags to remove</param>
        /// <param name="maxRemove">The maximum amount of tags to remove (tags can have the same index).<br/>Negative values will remove all tags at the given <paramref name="textIndex"/></param>
        /// <param name="coll">Removed tags will be appended to this collection</param>
        /// <exception cref="System.IndexOutOfRangeException">If the <paramref name="textIndex"/> is outside of the range of the text</exception>
        public void RemoveTagAtTextIndex(int textIndex, TMPAnimationType type, int maxRemove = 1, ICollection<TMPAnimationTag> coll = null)
        {
            if (textIndex < 0 || textIndex >= mediator.CharData.Count) throw new System.IndexOutOfRangeException();

            List<TMPAnimationTag> processor = null;
            List<CachedAnimation> cached = null;

            switch (type)
            {
                case TMPAnimationType.Basic:
                    processor = atp.ProcessedTags;
                    cached = basicCached;
                    break;

                case TMPAnimationType.Show:
                    processor = satp.ProcessedTags;
                    cached = showCached;
                    break;

                case TMPAnimationType.Hide:
                    processor = hatp.ProcessedTags;
                    cached = hideCached;
                    break;
            }

            if (maxRemove < 0) maxRemove = processor.Count;

            for (int i = 0; i < processor.Count; i++)
            {
                if (maxRemove <= 0) return;
                if (processor[i].startIndex == textIndex)
                {
                    var tag = processor[i];
                    processor.RemoveAt(i);
                    cached.Remove(cached.First(x => x.tag == tag));
                    coll?.Add(tag);
                    i--;
                }
            }
        }

        /// <summary>
        /// Clear all tags of the given type.
        /// </summary>
        /// <param name="type">The type of tag that is cleared.</param>
        public void ClearTags(TMPAnimationType type)
        {
            switch (type)
            {
                case TMPAnimationType.Basic:
                    atp.ProcessedTags.Clear();
                    basicCached.Clear();
                    break;
                case TMPAnimationType.Show:
                    satp.ProcessedTags.Clear();
                    showCached.Clear();
                    break;
                case TMPAnimationType.Hide:
                    hatp.ProcessedTags.Clear();
                    hideCached.Clear();
                    break;
            }
        }
        /// <summary>
        /// Clear all tags.
        /// </summary>
        public void ClearTags()
        {
            atp.ProcessedTags.Clear();
            basicCached.Clear();
            satp.ProcessedTags.Clear();
            showCached.Clear();
            hatp.ProcessedTags.Clear();
            hideCached.Clear();
        }

        /// <summary>
        /// Check whether the character is excluded from animations of the given type.
        /// </summary>
        /// <param name="c">The character to check</param>
        /// <param name="type">The type of animation to check against</param>
        /// <returns>Whether the character is excluded from animations of the given type</returns>
        /// <exception cref="System.ArgumentException">If an invalid <see cref="TMPAnimationType"/> is passed in</exception>
        public bool IsExcluded(char c, TMPAnimationType type)
        {
            switch (type)
            {
                case TMPAnimationType.Basic: return IsExcludedBasic(c);
                case TMPAnimationType.Show: return IsExcludedShow(c);
                case TMPAnimationType.Hide: return IsExcludedHide(c);
                default: throw new System.ArgumentException();
            }
        }
        /// <summary>
        /// Check whether the given character is excluded from basic animations.
        /// </summary>
        /// <param name="c">The character to check</param>
        /// <returns>Whether the character is excluded from basic animations</returns>
        public bool IsExcludedBasic(char c) => (excludePunctuation && char.IsPunctuation(c)) || excludedCharacters.Contains(c);
        /// <summary>
        /// Check whether the given character is excluded from show animations.
        /// </summary>
        /// <param name="c">The character to check</param>
        /// <returns>Whether the character is excluded from show animations</returns>
        public bool IsExcludedShow(char c) => (excludePunctuationShow && char.IsPunctuation(c)) || excludedCharactersShow.Contains(c);
        /// <summary>
        /// Check whether the given character is excluded from hide animations.
        /// </summary>
        /// <param name="c">The character to check</param>
        /// <returns>Whether the character is excluded from hide animations</returns>
        public bool IsExcludedHide(char c) => (excludePunctuationHide && char.IsPunctuation(c)) || excludedCharactersHide.Contains(c);

        private bool InsertTag_Impl(TMPAnimationType type, string key, Dictionary<string, string> parameters, int textIndex, int length)
        {
            if (textIndex < 0 || textIndex >= mediator.CharData.Count || (textIndex + length) > mediator.CharData.Count) throw new System.IndexOutOfRangeException();

            TMPAnimationTag t;
            switch (type)
            {
                case TMPAnimationType.Basic:
                    if (!ValidateAnimationTag(key, parameters, database.basicAnimationDatabase)) return false;
                    t = new(key, textIndex, GetOrderAtIndex(TMPAnimationType.Basic, textIndex), parameters);
                    break;

                case TMPAnimationType.Show:
                    if (!ValidateAnimationTag(key, parameters, database.showAnimationDatabase)) return false;
                    t = new(key, textIndex, GetOrderAtIndex(TMPAnimationType.Show, textIndex), parameters);
                    break;

                case TMPAnimationType.Hide:
                    if (!ValidateAnimationTag(key, parameters, database.hideAnimationDatabase)) return false;
                    t = new(key, textIndex, GetOrderAtIndex(TMPAnimationType.Hide, textIndex), parameters);
                    break;

                default:
                    Debug.LogError("Tag \"" + tag + "\" has invalid type");
                    return false;
            }

            if (length < 0) t.Close(mediator.CharData.Count - 1);
            else if (length == 0) t.Close(textIndex);
            else t.Close(textIndex + length - 1);
            CacheAnimation(t, type);

            return true;
        }
        private bool InsertTag_Impl(string tag, int textIndex, int length)
        {
            if (textIndex < 0 || textIndex >= mediator.CharData.Count || (textIndex + length) > mediator.CharData.Count) return false;

            ParsingUtility.TagInfo tagInfo = new ParsingUtility.TagInfo();
            bool parsed = ParsingUtility.TryParseTag(tag, 0, tag.Length - 1, ref tagInfo, ParsingUtility.TagType.Open);
            if (!parsed) return false;

            Dictionary<string, string> parameters;
            TMPAnimationTag t;
            switch (tagInfo.prefix)
            {
                case ParsingUtility.NO_PREFIX:
                    if (!ValidateAnimationTag(tag, tagInfo, database.basicAnimationDatabase, out parameters)) return false;
                    t = new(tagInfo.name, textIndex, GetOrderAtIndex(TMPAnimationType.Basic, textIndex), parameters);
                    CacheAnimation(t, TMPAnimationType.Basic);
                    break;

                case ParsingUtility.SHOW_ANIMATION_PREFIX:
                    if (!ValidateAnimationTag(tag, tagInfo, database.showAnimationDatabase, out parameters)) return false;
                    t = new(tagInfo.name, textIndex, GetOrderAtIndex(TMPAnimationType.Show, textIndex), parameters);
                    CacheAnimation(t, TMPAnimationType.Show);
                    break;

                case ParsingUtility.HIDE_ANIMATION_PREFIX:
                    if (!ValidateAnimationTag(tag, tagInfo, database.hideAnimationDatabase, out parameters)) return false;
                    t = new(tagInfo.name, textIndex, GetOrderAtIndex(TMPAnimationType.Hide, textIndex), parameters);
                    CacheAnimation(t, TMPAnimationType.Hide);
                    break;

                default:
                    Debug.LogError("Tag \"" + tag + "\" has invalid prefix \'" + tagInfo.prefix + "\'");
                    return false;
            }

            if (length < 0) t.Close(mediator.CharData.Count - 1);
            else if (length == 0) t.Close(textIndex);
            else t.Close(textIndex + length - 1);

            return true;
        }
        private void RemoveTag_Impl(TMPAnimationType type, int index)
        {
            if (index < 0) throw new System.IndexOutOfRangeException();

            List<TMPAnimationTag> processor = null;
            List<CachedAnimation> cached = null;

            switch (type)
            {
                case TMPAnimationType.Basic:
                    processor = atp.ProcessedTags;
                    cached = basicCached;
                    break;

                case TMPAnimationType.Show:
                    processor = satp.ProcessedTags;
                    cached = showCached;
                    break;

                case TMPAnimationType.Hide:
                    processor = hatp.ProcessedTags;
                    cached = hideCached;
                    break;
            }

            if (index >= processor.Count) throw new System.IndexOutOfRangeException();
            var tag = processor[index];
            cached.Remove(cached.First(x => x.tag == tag));
            processor.RemoveAt(index);
        }
        private int GetOrderAtIndex(TMPAnimationType type, int index)
        {
            int tmpIndex;
            switch (type)
            {
                case TMPAnimationType.Basic:
                    tmpIndex = basicCached.FindIndex(x => x.tag.startIndex == index);
                    if (tmpIndex == -1) return 0;
                    else return basicCached[tmpIndex].tag.orderAtIndex - 1;

                case TMPAnimationType.Show:
                    tmpIndex = showCached.FindIndex(x => x.tag.startIndex == index);
                    if (tmpIndex == -1) return 0;
                    else return showCached[tmpIndex].tag.orderAtIndex - 1;

                case TMPAnimationType.Hide:
                    tmpIndex = hideCached.FindIndex(x => x.tag.startIndex == index);
                    if (tmpIndex == -1) return 0;
                    else return hideCached[tmpIndex].tag.orderAtIndex - 1;
            }

            throw new System.ArgumentException(nameof(type));
        }
        private bool ValidateAnimationTag<T>(string tag, ParsingUtility.TagInfo tagInfo, TMPEffectDatabase<T> database, out Dictionary<string, string> parametersOut) where T : ITMPAnimation
        {
            parametersOut = null;
            if (!database.ContainsEffect(tagInfo.name)) return false;
            parametersOut = ParsingUtility.GetTagParametersDict(tag);
            if (!database.GetEffect(tagInfo.name).ValidateParameters(parametersOut)) return false;
            return true;
        }
        private bool ValidateAnimationTag<T>(string key, Dictionary<string, string> parameters, TMPEffectDatabase<T> database) where T : ITMPAnimation
        {
            if (!database.ContainsEffect(key)) return false;
            if (!database.GetEffect(key).ValidateParameters(parameters)) return false;
            return true;
        }
        #endregion

        #region Animations
        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            if (updateFrom == UpdateFrom.Update && isAnimating) UpdateAnimations_Impl(Time.deltaTime);
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            if (updateFrom == UpdateFrom.LateUpdate && isAnimating) UpdateAnimations_Impl(Time.deltaTime);
        }

        private void FixedUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            if (updateFrom == UpdateFrom.FixedUpdate && isAnimating) UpdateAnimations_Impl(Time.fixedDeltaTime);
        }

        private void UpdateAnimations_Impl(float deltaTime)
        {
            context.passedTime += deltaTime;

            for (int i = 0; i < mediator.CharData.Count; i++)
            {
                UpdateCharacterAnimation(deltaTime, i, false);
            }

            if (mediator.Text.mesh != null)
                mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private void UpdateCharacterAnimation(float deltaTime, int index, bool updateVertices = true)
        {
            CharData cData = mediator.CharData[index];
            if (!AnimateCharacter(ref cData)) return;

            context.deltaTime = deltaTime;


            UpdateCharacterAnimation_Impl(index);

            // TODO only set actually changed meshes; dirty flag on cdata & cehcking of uv vert color
            var info = mediator.Text.textInfo;
            TMP_CharacterInfo cInfo = info.characterInfo[index];
            int vIndex = cInfo.vertexIndex, mIndex = cInfo.materialReferenceIndex;
            Color32[] colors = info.meshInfo[mIndex].colors32;
            Vector3[] verts = info.meshInfo[mIndex].vertices;
            Vector2[] uvs0 = info.meshInfo[mIndex].uvs0;
            Vector2[] uvs2 = info.meshInfo[mIndex].uvs2;
            //Vector2[] uvs4 = info.meshInfo[mIndex].uvs2;

            for (int j = 0; j < 4; j++)
            {
                verts[vIndex + j] = mediator.CharData[index].mesh[j].position;
                colors[vIndex + j] = mediator.CharData[index].mesh[j].color;
                uvs0[vIndex + j] = mediator.CharData[index].mesh[j].uv;
                uvs2[vIndex + j] = mediator.CharData[index].mesh[j].uv2;
                //uvs4[vIndex + j] = mediator.CharData[index].currentMesh[j].uv4;
            }

            if (updateVertices && mediator.Text.mesh != null)
                mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private bool AnimateCharacter(ref CharData cData) => cData.info.isVisible && cData.visibilityState != CharData.VisibilityState.Hidden;

        /// <summary>
        /// Get all animations (as indeces of the respective collection) that are active at the given index.<br/>
        /// In desecending order by default.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="type"></param>
        /// <param name="ascending"></param>
        /// <returns></returns>
        private IEnumerable<int> ActiveAtIndex(int index, TMPAnimationType type, bool ascending = false)
        {
            List<CachedAnimation> anims = null;
            switch (type)
            {
                case TMPAnimationType.Basic: anims = basicCached; break;
                case TMPAnimationType.Show: anims = showCached; break;
                case TMPAnimationType.Hide: anims = hideCached; break;
            }

            int lastIndex = anims.FindIndex(x => x.tag.startIndex > index) - 1;
            if (lastIndex < -1) lastIndex = anims.Count - 1;

            for (; lastIndex >= 0; lastIndex--)
            {
                if (anims[lastIndex].tag.endIndex <= index) continue;
                yield return lastIndex;
            }
        }

        private IEnumerable<CachedAnimation> CachedActiveAtIndex(int index, TMPAnimationType type)
        {
            List<CachedAnimation> anims = null;
            switch (type)
            {
                case TMPAnimationType.Basic: anims = basicCached; break;
                case TMPAnimationType.Show: anims = showCached; break;
                case TMPAnimationType.Hide: anims = hideCached; break;
            }

            int lastIndex = anims.FindIndex(x => x.tag.startIndex > index) - 1;
            if (lastIndex < -1) lastIndex = anims.Count - 1;

            for (; lastIndex >= 0; lastIndex--)
            {
                if (anims[lastIndex].tag.endIndex < index) continue;
                yield return anims[lastIndex];
            }
        }

        private int UpdateCharacterAnimation_Impl(int index)
        {
            CharData cData = mediator.CharData[index];
            if (!cData.info.isVisible || cData.visibilityState == CharData.VisibilityState.Hidden) return 0;

            int applied = 0;

            Vector3 positionDelta = Vector3.zero;
            Matrix4x4 scaleDelta = Matrix4x4.Scale(Vector3.one);
            Quaternion rotation = Quaternion.identity;
            Vector3 rotationPivot = cData.info.initialPosition;

            Vector3 TL = Vector3.zero;
            Vector3 TR = Vector3.zero;
            Vector3 BR = Vector3.zero;
            Vector3 BL = Vector3.zero;

            Vector3 TLMax = cData.mesh.initial.vertex_TL.position;
            Vector3 TRMax = cData.mesh.initial.vertex_TR.position;
            Vector3 BRMax = cData.mesh.initial.vertex_BR.position;
            Vector3 BLMax = cData.mesh.initial.vertex_BL.position;

            Vector3 TLMin = cData.mesh.initial.vertex_TL.position;
            Vector3 TRMin = cData.mesh.initial.vertex_TR.position;
            Vector3 BRMin = cData.mesh.initial.vertex_BR.position;
            Vector3 BLMin = cData.mesh.initial.vertex_BL.position;

            Vector2 TL_UV = cData.mesh.initial.vertex_TL.uv;
            Vector2 TR_UV = cData.mesh.initial.vertex_TR.uv;
            Vector2 BR_UV = cData.mesh.initial.vertex_BR.uv;
            Vector2 BL_UV = cData.mesh.initial.vertex_BL.uv;

            Vector2 TL_UV2 = cData.mesh.initial.vertex_TL.uv2;
            Vector2 TR_UV2 = cData.mesh.initial.vertex_TR.uv2;
            Vector2 BR_UV2 = cData.mesh.initial.vertex_BR.uv2;
            Vector2 BL_UV2 = cData.mesh.initial.vertex_BL.uv2;

            Color32 TL_Color = cData.mesh.initial.vertex_TL.color;
            Color32 TR_Color = cData.mesh.initial.vertex_TR.color;
            Color32 BR_Color = cData.mesh.initial.vertex_BR.color;
            Color32 BL_Color = cData.mesh.initial.vertex_BL.color;

            if (cData.visibilityState == CharData.VisibilityState.ShowAnimation)
            {
                if (!IsExcludedShow(cData.info.character))
                {
                    AnimateList(TMPAnimationType.Show);
                }
            }
            else if (cData.visibilityState == CharData.VisibilityState.HideAnimation)
            {
                if (!IsExcludedHide(cData.info.character))
                {
                    AnimateList(TMPAnimationType.Hide);
                }
            }

            if (cData.visibilityState == CharData.VisibilityState.Hidden || IsExcludedBasic(cData.info.character))
            {
                ApplyVertices();
                return 1;
            }

            AnimateList(TMPAnimationType.Basic);

            //if (animationsOverride)
            //{
            //    int animIndex = GetActiveIndex(index, basicCached);
            //    if (animIndex >= 0) Animate(basicCached[animIndex]);
            //}
            //else
            //{
            //    // Get all basic animations active here and apply
            //    CachedAnimation ca;
            //    for (int i = 0; i < basicCached.Count; i++)
            //    {
            //        ca = basicCached[i];
            //        if (ca.tag.startIndex <= index && ca.tag.startIndex + ca.tag.length > index)
            //        {
            //            Animate(ca);
            //        }
            //    }
            //}

            ApplyVertices();
            return applied;

            void Animate(CachedAnimation ca)
            {
                cData.Reset();
                cData.segmentIndex = index - ca.tag.startIndex;
                if (cData.segmentIndex == 0)
                {
                    // cdata is first
                }
                else
                {

                }

                for (int i = 0; i < 4; i++)
                {
                    cData.mesh.SetPosition(i, cData.mesh.initial.GetPosition(i)); // cData.initialMesh.GetPosition(i));
                }

                ca.animation.ResetParameters();
                ca.animation.SetParameters(ca.tag.parameters);
                ca.animation.Animate(ref cData, ref ca.context);

                UpdateVertexOffsets();

                applied++;
            }

            void AnimateList(TMPAnimationType type)
            {
                switch (type)
                {
                    case TMPAnimationType.Show:
                        if (showCached.Count == 0)
                        {
                            Animate(defaultShow);
                            return;
                        }
                        break;

                    case TMPAnimationType.Hide:
                        if (hideCached.Count == 0)
                        {
                            Animate(defaultHide);
                            return;
                        }
                        break;
                }

                if (animationsOverride)
                {
                    foreach (CachedAnimation animation in CachedActiveAtIndex(index, type))
                    {
                        Animate(animation);
                        if (!(animation.overrides != null && !animation.overrides.Value))
                            break;
                    }
                }
                else
                {
                    foreach (CachedAnimation animation in CachedActiveAtIndex(index, type))
                    {
                        Animate(animation);
                        if (animation.overrides != null && animation.overrides.Value)
                            break;
                    }
                }
            }

            void UpdateVertexOffsets()
            {
                if (cData.positionDirty) positionDelta += (cData.Position - cData.info.initialPosition);
                if (cData.scaleDirty)
                {
                    scaleDelta *= Matrix4x4.Scale(cData.Scale);
                }
                if (cData.rotationDirty)
                {
                    rotation = cData.Rotation * rotation;
                    rotationPivot += (cData.RotationPivot - cData.info.initialPosition);
                }

                if (cData.verticesDirty)
                {
                    Vector3 deltaTL = (cData.mesh.vertex_TL.position - cData.mesh.initial.vertex_TL.position) * (context.scaleAnimations ? cData.info.referenceScale : 1);
                    Vector3 deltaTR = (cData.mesh.vertex_TR.position - cData.mesh.initial.vertex_TR.position) * (context.scaleAnimations ? cData.info.referenceScale : 1);
                    Vector3 deltaBR = (cData.mesh.vertex_BR.position - cData.mesh.initial.vertex_BR.position) * (context.scaleAnimations ? cData.info.referenceScale : 1);
                    Vector3 deltaBL = (cData.mesh.vertex_BL.position - cData.mesh.initial.vertex_BL.position) * (context.scaleAnimations ? cData.info.referenceScale : 1);

                    TL += deltaTL;// (cData.currentMesh.vertex_TL.position - cData.mesh.initial.vertex_TL.position);
                    TR += deltaTR;// (cData.currentMesh.vertex_TR.position - cData.mesh.initial.vertex_TR.position);
                    BR += deltaBR;// (cData.currentMesh.vertex_BR.position - cData.mesh.initial.vertex_BR.position);
                    BL += deltaBL;// (cData.currentMesh.vertex_BL.position - cData.mesh.initial.vertex_BL.position);

                    TLMax = new Vector3(Mathf.Max(cData.mesh.initial.vertex_TL.position.x + deltaTL.x, TLMax.x), Mathf.Max(cData.mesh.initial.vertex_TL.position.y + deltaTL.y, TLMax.y), Mathf.Max(cData.mesh.initial.vertex_TL.position.z + deltaTL.z, TLMax.z));
                    TRMax = new Vector3(Mathf.Max(cData.mesh.initial.vertex_TR.position.x + deltaTR.x, TRMax.x), Mathf.Max(cData.mesh.initial.vertex_TR.position.y + deltaTR.y, TRMax.y), Mathf.Max(cData.mesh.initial.vertex_TR.position.z + deltaTR.z, TRMax.z));
                    BRMax = new Vector3(Mathf.Max(cData.mesh.initial.vertex_BR.position.x + deltaBR.x, BRMax.x), Mathf.Max(cData.mesh.initial.vertex_BR.position.y + deltaBR.y, BRMax.y), Mathf.Max(cData.mesh.initial.vertex_BR.position.z + deltaBR.z, BRMax.z));
                    BLMax = new Vector3(Mathf.Max(cData.mesh.initial.vertex_BL.position.x + deltaBL.x, BLMax.x), Mathf.Max(cData.mesh.initial.vertex_BL.position.y + deltaBL.y, BLMax.y), Mathf.Max(cData.mesh.initial.vertex_BL.position.z + deltaBL.z, BLMax.z));

                    TLMin = new Vector3(Mathf.Min(cData.mesh.initial.vertex_TL.position.x + deltaTL.x, TLMin.x), Mathf.Min(cData.mesh.initial.vertex_TL.position.y + deltaTL.y, TLMin.y), Mathf.Min(cData.mesh.initial.vertex_TL.position.z + deltaTL.z, TLMin.z));
                    TRMin = new Vector3(Mathf.Min(cData.mesh.initial.vertex_TR.position.x + deltaTR.x, TRMin.x), Mathf.Min(cData.mesh.initial.vertex_TR.position.y + deltaTR.y, TRMin.y), Mathf.Min(cData.mesh.initial.vertex_TR.position.z + deltaTR.z, TRMin.z));
                    BRMin = new Vector3(Mathf.Min(cData.mesh.initial.vertex_BR.position.x + deltaBR.x, BRMin.x), Mathf.Min(cData.mesh.initial.vertex_BR.position.y + deltaBR.y, BRMin.y), Mathf.Min(cData.mesh.initial.vertex_BR.position.z + deltaBR.z, BRMin.z));
                    BLMin = new Vector3(Mathf.Min(cData.mesh.initial.vertex_BL.position.x + deltaBL.x, BLMin.x), Mathf.Min(cData.mesh.initial.vertex_BL.position.y + deltaBL.y, BLMin.y), Mathf.Min(cData.mesh.initial.vertex_BL.position.z + deltaBL.z, BLMin.z));
                }

                if (cData.colorsDirty)
                {
                    BL_Color = cData.mesh.GetColor(0);
                    TL_Color = cData.mesh.GetColor(1);
                    TR_Color = cData.mesh.GetColor(2);
                    BR_Color = cData.mesh.GetColor(3);
                }

                if (cData.uvsDirty)
                {
                    BL_UV = cData.mesh.GetUV0(0);
                    TL_UV = cData.mesh.GetUV0(1);
                    TR_UV = cData.mesh.GetUV0(2);
                    BR_UV = cData.mesh.GetUV0(3);

                    BL_UV2 = cData.mesh.GetUV2(0);
                    TL_UV2 = cData.mesh.GetUV2(1);
                    TR_UV2 = cData.mesh.GetUV2(2);
                    BR_UV2 = cData.mesh.GetUV2(3);
                }
            }

            void ApplyVertices()
            {
                // Apply vertex transformations
                Vector3 vtl = cData.mesh.initial.vertex_TL.position + TL;// * (context.scaleAnimations ? cData.info.referenceScale : 1);
                Vector3 vtr = cData.mesh.initial.vertex_TR.position + TR;// * (context.scaleAnimations ? cData.info.referenceScale : 1);
                Vector3 vbr = cData.mesh.initial.vertex_BR.position + BR;// * (context.scaleAnimations ? cData.info.referenceScale : 1);
                Vector3 vbl = cData.mesh.initial.vertex_BL.position + BL;// * (context.scaleAnimations ? cData.info.referenceScale : 1);

                // TODO Does this make sense?
                // For now only the vertex offsets are clamped to min/max of each individual animation, as otherwise stacked animations are likely to deform the character
                vtl = new Vector3(Mathf.Clamp(vtl.x, TLMin.x, TLMax.x), Mathf.Clamp(vtl.y, TLMin.y, TLMax.y), Mathf.Clamp(vtl.z, TLMin.z, TLMax.z));
                vtr = new Vector3(Mathf.Clamp(vtr.x, TRMin.x, TRMax.x), Mathf.Clamp(vtr.y, TRMin.y, TRMax.y), Mathf.Clamp(vtr.z, TRMin.z, TRMax.z));
                vbr = new Vector3(Mathf.Clamp(vbr.x, BRMin.x, BRMax.x), Mathf.Clamp(vbr.y, BRMin.y, BRMax.y), Mathf.Clamp(vbr.z, BRMin.z, BRMax.z));
                vbl = new Vector3(Mathf.Clamp(vbl.x, BLMin.x, BLMax.x), Mathf.Clamp(vbl.y, BLMin.y, BLMax.y), Mathf.Clamp(vbl.z, BLMin.z, BLMax.z));

                // Apply scale
                vtl = scaleDelta.MultiplyPoint3x4(vtl - cData.info.initialPosition) + cData.info.initialPosition;
                vtr = scaleDelta.MultiplyPoint3x4(vtr - cData.info.initialPosition) + cData.info.initialPosition;
                vbr = scaleDelta.MultiplyPoint3x4(vbr - cData.info.initialPosition) + cData.info.initialPosition;
                vbl = scaleDelta.MultiplyPoint3x4(vbl - cData.info.initialPosition) + cData.info.initialPosition;

                // Apply rotation
                Matrix4x4 matrix = Matrix4x4.Rotate(rotation);
                vtl = matrix.MultiplyPoint3x4(vtl - rotationPivot) + rotationPivot;
                vtr = matrix.MultiplyPoint3x4(vtr - rotationPivot) + rotationPivot;
                vbr = matrix.MultiplyPoint3x4(vbr - rotationPivot) + rotationPivot;
                vbl = matrix.MultiplyPoint3x4(vbl - rotationPivot) + rotationPivot;

                // Apply transformation
                vtl += positionDelta * (context.scaleAnimations ? cData.info.referenceScale : 1);
                vtr += positionDelta * (context.scaleAnimations ? cData.info.referenceScale : 1);
                vbr += positionDelta * (context.scaleAnimations ? cData.info.referenceScale : 1);
                vbl += positionDelta * (context.scaleAnimations ? cData.info.referenceScale : 1);

                cData.mesh.SetPosition(0, vbl);
                cData.mesh.SetPosition(1, vtl);
                cData.mesh.SetPosition(2, vtr);
                cData.mesh.SetPosition(3, vbr);

                cData.mesh.SetColor(0, BL_Color);
                cData.mesh.SetColor(1, TL_Color);
                cData.mesh.SetColor(2, TR_Color);
                cData.mesh.SetColor(3, BR_Color);

                cData.mesh.SetUV(0, BL_UV);
                cData.mesh.SetUV(1, TL_UV);
                cData.mesh.SetUV(2, TR_UV);
                cData.mesh.SetUV(3, BR_UV);

                cData.mesh.SetUV2(0, BL_UV2);
                cData.mesh.SetUV2(1, TL_UV2);
                cData.mesh.SetUV2(2, TR_UV2);
                cData.mesh.SetUV2(3, BR_UV2);

                mediator.CharData[index] = cData;
            }
        }

        private int GetActiveIndex(int charIndex, IList<CachedAnimation> animations)
        {
            int maxStart = -1; int maxIndex = -1;
            for (int i = 0; i < animations.Count; i++)
            {
                if (animations[i].tag.startIndex >= maxStart && animations[i].tag.startIndex <= charIndex && animations[i].tag.startIndex + animations[i].tag.length > charIndex)
                {
                    maxStart = animations[i].tag.startIndex;
                    maxIndex = i;
                }
            }

            return maxIndex;
        }
        #endregion

        #region Event Callbacks
        private void OnTextChanged()
        {
            if (isAnimating) UpdateAnimations_Impl(0f);
        }

        private void OnForcedUpdate(int start, int length)
        {
            if (!isAnimating) return;

            for (int i = 0; i < length; i++)
            {
                UpdateCharacterAnimation(0f, i + start);
            }
        }
        #endregion

        const string falseUpdateAnimationsCallWarning = "Called UpdateAnimations while TMPAnimator {0} is set to automatically update from {1}; " +
            "If you want to manually control the animation updates, set its UpdateFrom property to \"Script\", " +
            "either through the inspector or through a script using the SetUpdateFrom method.";
        const string falseStartStopAnimatingCallWarning = "Called {0} while TMPAnimator {1} is set to manually update from script; " +
            "If you want the TMPAnimator to automatically update and to use the Start / StopAnimating methods, set its UpdateFrom property to \"Update\", \"LateUpdate\" or \"FixedUpdate\", " +
            "either through the inspector or through a script using the SetUpdateFrom method.";

        #region Editor Only
#if UNITY_EDITOR
        [SerializeField, HideInInspector] bool preview = false;
        [SerializeField, HideInInspector] bool initDatabase = false;
        [SerializeField, HideInInspector] bool startedEditorApplication = false;
        [SerializeField, HideInInspector] TMPAnimationDatabase prevDatabase = null;

        public void StartPreview()
        {
            //preview = true;
            StartAnimating();

            EditorApplication.update -= UpdatePreview;
            EditorApplication.update += UpdatePreview;
        }

        public void StopPreview()
        {
            //preview = false;
            EditorApplication.update -= UpdatePreview;
            if (updateFrom != UpdateFrom.Script) StopAnimating();
            ResetAnimations();
        }

        [System.NonSerialized, HideInInspector] float lastTimeSinceStartUp = 0;
        private void UpdatePreview()
        {

            UpdateAnimations_Impl((float)EditorApplication.timeSinceStartup - context.passedTime);
            EditorApplication.QueuePlayerLoopUpdate();
        }

        public void ForceReprocess()
        {
            if (mediator != null) mediator.ForceReprocess();
        }

        public void ForcePostProcess()
        {
            if (mediator != null) PostProcessTags();
        }

        public void UpdateProcessorsWrapper()
        {
            if (mediator == null) return;
            UpdateProcessors();
        }

        public string CheckDefaultShowString()
        {
            if (database == null) return "No show database assigned on animation database";

            ParsingUtility.TagInfo tagInfo = new ParsingUtility.TagInfo();
            Dictionary<string, string> tagParams = null;
            ITMPAnimation animation;

            if (string.IsNullOrWhiteSpace(defaultShowString)) return "";

            string str = defaultShowString;
            str = (str.Trim()[0] == '<' ? str : "<" + str + ">");
            if (!ParsingUtility.TryParseTag(str, 0, str.Length - 1, ref tagInfo, ParsingUtility.TagType.Open)) return "Not a wellformed tag";
            if (!database.showAnimationDatabase.ContainsEffect(tagInfo.name)) return "Tag not contained within show database";

            if ((animation = database.showAnimationDatabase.GetEffect(tagInfo.name)) == null) return "Tag is valid but is not assigned an animation object within the show database";

            tagParams = ParsingUtility.GetTagParametersDict(str);
            if (!animation.ValidateParameters(tagParams)) return "Parameters are not valid for this tag";

            return "";
        }

        public string CheckDefaultHideString()
        {
            if (database == null) return "No hide database assigned on animation database";

            ParsingUtility.TagInfo tagInfo = new ParsingUtility.TagInfo();
            Dictionary<string, string> tagParams = null;
            ITMPAnimation animation;

            if (string.IsNullOrWhiteSpace(defaultHideString)) return "";

            string str = defaultHideString;
            str = (str.Trim()[0] == '<' ? str : "<" + str + ">");
            if (!ParsingUtility.TryParseTag(str, 0, str.Length - 1, ref tagInfo, ParsingUtility.TagType.Open)) return "Not a wellformed tag";
            if (!database.showAnimationDatabase.ContainsEffect(tagInfo.name)) return "Tag not contained within hide database";

            if ((animation = database.showAnimationDatabase.GetEffect(tagInfo.name)) == null) return "Tag is valid but is not assigned an animation object within the hide database";

            tagParams = ParsingUtility.GetTagParametersDict(str);
            if (!animation.ValidateParameters(tagParams)) return "Parameters are not valid for this tag";

            return "";
        }

        private void OnValidate()
        {
            if (mediator == null) return;

            if (prevDatabase != database || (database != null && (atp.Database != database.basicAnimationDatabase || satp.Database != database.showAnimationDatabase || hatp.Database != database.hideAnimationDatabase)))
            {
                prevDatabase = database;
                UpdateProcessors();
            }
        }
#endif
        #endregion

        private void UpdateProcessors()
        {
            mediator.Processor.UnregisterProcessor(ParsingUtility.NO_PREFIX);
            mediator.Processor.UnregisterProcessor(ParsingUtility.SHOW_ANIMATION_PREFIX);
            mediator.Processor.UnregisterProcessor(ParsingUtility.HIDE_ANIMATION_PREFIX);
            atp = new(database == null ? null : database.basicAnimationDatabase);
            satp = new(database == null ? null : database.showAnimationDatabase);
            hatp = new(database == null ? null : database.hideAnimationDatabase);
            mediator.Processor.RegisterProcessor(ParsingUtility.NO_PREFIX, atp);
            mediator.Processor.RegisterProcessor(ParsingUtility.SHOW_ANIMATION_PREFIX, satp);
            mediator.Processor.RegisterProcessor(ParsingUtility.HIDE_ANIMATION_PREFIX, hatp);
        }

        private struct CachedAnimation
        {
            public TMPAnimationTag tag;
            public bool? overrides;
            public ITMPAnimation animation;
            public IAnimationContext context;

            public CachedAnimation(TMPAnimator animator, TMPAnimationTag tag, ITMPAnimation animation, AnimatorContext animatorContext, TMPMediator mediator)
            {
                this.tag = tag;
                overrides = null;
                if (tag.parameters != null)
                {
                    bool tmp;
                    foreach (var param in tag.parameters.Keys)
                    {
                        switch (param)
                        {
                            case "override":
                            case "or":
                                if (ParsingUtility.StringToBool(tag.parameters[param], out tmp)) overrides = tmp;
                                // TODO remove it from parameters?
                                break;
                        }
                    }
                }

                this.animation = animation;
                this.context = animation.GetNewContext();
                if (context != null) context.animatorContext = animatorContext;

                context.segmentData = new SegmentData(animator, tag, mediator.CharData);
            }
        }

        private List<CachedAnimation> basicCached;
        private List<CachedAnimation> showCached;
        private List<CachedAnimation> hideCached;

        // TODO use this whereever applicable
        private void CacheAnimation(TMPAnimationTag tag, TMPAnimationType type)
        {
            switch (type)
            {
                case TMPAnimationType.Basic:
                    if (!atp.ProcessedTags.Contains(tag)) InsertElement(atp.ProcessedTags, tag);
                    InsertElement(basicCached, new CachedAnimation(this, tag, database.basicAnimationDatabase.GetEffect(tag.name), context, mediator), x => x.tag.startIndex >= tag.startIndex);
                    break;
                case TMPAnimationType.Show:
                    if (!satp.ProcessedTags.Contains(tag)) InsertElement(satp.ProcessedTags, tag);
                    InsertElement(showCached, new CachedAnimation(this, tag, database.showAnimationDatabase.GetEffect(tag.name), context, mediator), x => x.tag.startIndex >= tag.startIndex);
                    break;
                case TMPAnimationType.Hide:
                    if (!hatp.ProcessedTags.Contains(tag)) InsertElement(hatp.ProcessedTags, tag);
                    InsertElement(hideCached, new CachedAnimation(this, tag, database.hideAnimationDatabase.GetEffect(tag.name), context, mediator), x => x.tag.startIndex >= tag.startIndex);
                    break;
            }
        }

        private void PostProcessTags(/*string text*/)
        {
            // Close any unclosed animation tags
            int endIndex = mediator.CharData.Count - 1;
            foreach (var tag in Tags)
            {
                if (tag.IsOpen) tag.Close(endIndex);
            }

            // Cache the corresponding animation for each tag
            basicCached = new List<CachedAnimation>();
            showCached = new List<CachedAnimation>();
            hideCached = new List<CachedAnimation>();

            foreach (var tag in atp.ProcessedTags) basicCached.Add(new CachedAnimation(this, tag, database.basicAnimationDatabase.GetEffect(tag.name), context, mediator));
            foreach (var tag in satp.ProcessedTags) showCached.Add(new CachedAnimation(this, tag, database.showAnimationDatabase.GetEffect(tag.name), context, mediator));
            foreach (var tag in hatp.ProcessedTags) hideCached.Add(new CachedAnimation(this, tag, database.hideAnimationDatabase.GetEffect(tag.name), context, mediator));

            SetDummyShow();
            SetDummyHide();

            // Add default show / hide animation
            if (database == null || !SetDefault(TMPAnimationType.Show) /*!AddDefault(defaultShowString, database.showAnimationDatabase, showCached)*/)
            {
                defaultShow = dummyShow;
            }

            if (database == null || !SetDefault(TMPAnimationType.Hide) /*!AddDefault(defaultHideString, database.hideAnimationDatabase, hideCached)*/)
            {
                defaultHide = dummyHide;
            }
        }

        private void SetDummyShow()
        {
            TMPAnimationTag tag = new TMPAnimationTag("Dummy Show Animation", 0, 0, null);
            tag.Close(mediator.CharData.Count - 1);
            var cached = new CachedAnimation(this, tag, ScriptableObject.CreateInstance<DummyShowAnimation>(), context, mediator);
            dummyShow = cached;
        }

        private void SetDummyHide()
        {
            TMPAnimationTag tag = new TMPAnimationTag("Dummy Hide Animation", 0, 0, null);
            tag.Close(mediator.CharData.Count - 1);
            var cached = new CachedAnimation(this, tag, ScriptableObject.CreateInstance<DummyShowAnimation>(), context, mediator);
            dummyShow = cached;
        }

        // TOOD Update editor only check
        private bool SetDefault(TMPAnimationType type)
        {
            string str;
            CachedAnimation anim;

            switch (type)
            {
                case TMPAnimationType.Show:
                    str = defaultShowString;
                    break;

                case TMPAnimationType.Hide:
                    str = defaultHideString;
                    break;

                default:
                    throw new System.ArgumentException(nameof(type));
            }

            ParsingUtility.TagInfo tagInfo = new ParsingUtility.TagInfo();
            Dictionary<string, string> tagParams;
            ITMPAnimation animation;

            if (string.IsNullOrWhiteSpace(str)) return false;
            str = (str.Trim()[0] == '<' ? str : "<" + str + ">");
            if (!ParsingUtility.TryParseTag(str, 0, str.Length - 1, ref tagInfo, ParsingUtility.TagType.Open) || !database.ContainsEffect(tagInfo.name)) return false;

            if ((animation = database.GetEffect(tagInfo.name, type)) == null)
            {
                return false;
            }

            tagParams = ParsingUtility.GetTagParametersDict(str);
            if (!animation.ValidateParameters(tagParams)) return false;

            var cached = new CachedAnimation(this, new TMPAnimationTag(tagInfo.name, 0, 0, tagParams), animation, context, mediator);
            cached.tag.Close(mediator.CharData.Count - 1);

            switch (type)
            {
                case TMPAnimationType.Show:
                    defaultShow = cached;
                    break;

                case TMPAnimationType.Hide:
                    defaultHide = cached;
                    break;
            }

            return true;
        }

        //private bool AddDefault<T>(string str, TMPAnimationDatabaseBase<T> database, List<CachedAnimation> anims) where T : ITMPAnimation
        //{
        //    if (database == null) return false;

        //    ParsingUtility.TagInfo tagInfo = new ParsingUtility.TagInfo();
        //    Dictionary<string, string> tagParams = null;
        //    ITMPAnimation animation;

        //    if (string.IsNullOrWhiteSpace(str)) return false;

        //    str = (str.Trim()[0] == '<' ? str : "<" + str + ">");
        //    if (!ParsingUtility.TryParseTag(str, 0, str.Length - 1, ref tagInfo, ParsingUtility.TagType.Open) || !database.Contains(tagInfo.name)) return false;

        //    if ((animation = database.GetEffect(tagInfo.name)) == null)
        //    {
        //        return false;
        //    }

        //    tagParams = ParsingUtility.GetTagParametersDict(str);
        //    if (!animation.ValidateParameters(tagParams)) return false;

        //    var cached = new CachedAnimation(this, new TMPAnimationTag(tagInfo.name, 0, 0, tagParams), animation, context, mediator);
        //    cached.tag.Close(mediator.CharData.Count - 1);
        //    //anims.Insert(0, cached);

        //    return true;
        //}

        #region Dummy Animations
        private class DummyShowAnimation : TMPShowAnimation
        {
            public override void Animate(ref CharData cData, ref IAnimationContext context)
            {
                for (int i = 0; i < 4; i++)
                {
                    cData.mesh.SetPosition(i, cData.mesh.initial.GetPosition(i));
                }

                cData.SetVisibilityState(CharData.VisibilityState.Shown, context.animatorContext.passedTime);
            }

            public override void ResetParameters()
            {
            }

            public override void SetParameters(Dictionary<string, string> parameters)
            {
            }

            public override bool ValidateParameters(Dictionary<string, string> parameters)
            {
                return true;
            }
        }
        private class DummyHideAnimation : TMPShowAnimation
        {
            public override void Animate(ref CharData cData, ref IAnimationContext context)
            {
                for (int i = 0; i < 4; i++)
                {
                    EffectUtility.SetVertexRaw(i, cData.info.initialPosition, ref cData, ref context);
                }

                cData.SetVisibilityState(CharData.VisibilityState.Hidden, context.animatorContext.passedTime);
            }

            public override void ResetParameters()
            {
            }

            public override void SetParameters(Dictionary<string, string> parameters)
            {
            }

            public override bool ValidateParameters(Dictionary<string, string> parameters)
            {
                return true;
            }
        }
        #endregion

        private void ResetAllVisible()
        {
            var info = mediator.Text.textInfo;

            Vector3[] verts;
            Color32[] colors;
            int vIndex, mIndex;
            TMP_CharacterInfo cInfo;

            // Iterate over all characters and apply the new meshes
            for (int i = 0; i < info.characterCount; i++)
            {
                cInfo = info.characterInfo[i];
                if (!cInfo.isVisible || mediator.CharData[i].visibilityState == CharData.VisibilityState.Hidden/*hidden*/) continue;

                vIndex = cInfo.vertexIndex;
                mIndex = cInfo.materialReferenceIndex;

                colors = info.meshInfo[mIndex].colors32;
                verts = info.meshInfo[mIndex].vertices;

                CharData cData = mediator.CharData[i];

                for (int j = 0; j < 4; j++)
                {
                    cData.mesh.SetPosition(j, cData.mesh.initial.GetPosition(j));
                    cData.mesh.SetColor(j, cData.mesh.initial.GetColor(j));

                    verts[vIndex + j] = mediator.CharData[i].mesh.initial[j].position;
                    colors[vIndex + j] = mediator.CharData[i].mesh.initial[j].color;
                }

                mediator.CharData[i] = cData;
            }

            if (mediator.Text.mesh != null)
                mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private void EnsureCorrectTiming(int index, CharData.VisibilityState prev)
        {
            if (context == null) return;
            float passed = context.passedTime;
            CharData cData = mediator.CharData[index];
            CharData.VisibilityState current = cData.visibilityState;
            cData.SetVisibilityState(prev, -1);
            cData.SetVisibilityState(current, passed);
            mediator.CharData[index] = cData;
        }
    }
}