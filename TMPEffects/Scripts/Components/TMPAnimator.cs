using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using TMPEffects.Animations;
using TMPEffects.Databases;
using TMPEffects.TextProcessing;
using System;
using System.Collections.ObjectModel;
using TMPEffects.Extensions;

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

        public ITagCollection Tags => tags;
        public ITagCollection BasicTags => tags[basicCategory];
        public ITagCollection ShowTags => tags[showCategory];
        public ITagCollection HideTags => tags[hideCategory];

        public bool AnimateOnStart { get => animateOnStart; set => animateOnStart = value; }
        public bool AnimationsOverride { get => animationsOverride; set => animationsOverride = value; }

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

        [System.NonSerialized] private TagProcessorManager processors;
        [System.NonSerialized] private TagCollectionManager<TMPAnimationCategory> tags;
        [System.NonSerialized] private bool isAnimating = false;

        [System.NonSerialized] private TMPAnimationCategory basicCategory;
        [System.NonSerialized] private TMPAnimationCategory showCategory;
        [System.NonSerialized] private TMPAnimationCategory hideCategory;

        [System.NonSerialized] private CachedCollection<CachedAnimation> basic;
        [System.NonSerialized] private CachedCollection<CachedAnimation> show;
        [System.NonSerialized] private CachedCollection<CachedAnimation> hide;

        [System.NonSerialized] private CachedAnimation dummyShow;
        [System.NonSerialized] private CachedAnimation dummyHide;
        [System.NonSerialized] private CachedAnimation defaultShow;
        [System.NonSerialized] private CachedAnimation defaultHide;

        const string falseUpdateAnimationsCallWarning = "Called UpdateAnimations while TMPAnimator {0} is set to automatically update from {1}; " +
            "If you want to manually control the animation updates, set its UpdateFrom property to \"Script\", " +
            "either through the inspector or through a script using the SetUpdateFrom method.";
        const string falseStartStopAnimatingCallWarning = "Called {0} while TMPAnimator {1} is set to manually update from script; " +
            "If you want the TMPAnimator to automatically update and to use the Start / StopAnimating methods, set its UpdateFrom property to \"Update\", \"LateUpdate\" or \"FixedUpdate\", " +
            "either through the inspector or through a script using the SetUpdateFrom method.";
        #endregion

        #region Initialization
        private void OnEnable()
        {
            UpdateMediator();

            SetDummyShow();
            SetDummyHide();

            PrepareForProcessing();

            SubscribeToMediator();

            Mediator.ForceReprocess();

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

        private void OnDisable()
        {
            processors.UnregisterFrom(Mediator.Processor);

            Mediator.ForceReprocess();

#if UNITY_EDITOR
            StopPreview();
#endif

            UnsubscribeFromMediator();
        }

        private void SubscribeToMediator()
        {
            //Mediator.Subscribe(this); // Subscribe to the mediator; This makes the mediator persistent at least until this component is destroyed

            Mediator.OnVisibilityStateUpdated += EnsureCorrectTiming; // Ensure visibility time of object is persistent with context.UseScaledTime; TODO: likely move this into mediator or TMPEffectComponent
            Mediator.CharDataPopulated += PostProcessTags; // 
            Mediator.TextChanged += OnTextChanged; // Will update animations once; otherwise, depending on timing of text change, there'll be a frame of unanimated text
            Mediator.ForcedUpdate += OnForcedUpdate; // Will update animations at indices once; otherwise, depending on timing of required update, there'll be a frame of unanimated text
            //mediator.Disabled += DisableComponent;
        }

        private void UnsubscribeFromMediator()
        {
            Mediator.OnVisibilityStateUpdated -= EnsureCorrectTiming;
            Mediator.CharDataPopulated -= PostProcessTags;
            Mediator.TextChanged -= OnTextChanged;
            Mediator.ForcedUpdate -= OnForcedUpdate;
            //mediator.Disabled -= DisableComponent;

            FreeMediator();
            //Mediator.Unsubscribe(this);
        }

        private void PrepareForProcessing()
        {
            // Reset categories
            basicCategory = new TMPAnimationCategory(ParsingUtility.ANIMATION_PREFIX, database == null ? null : database.basicAnimationDatabase);
            showCategory = new TMPAnimationCategory(ParsingUtility.SHOW_ANIMATION_PREFIX, database == null ? null : database.showAnimationDatabase);
            hideCategory = new TMPAnimationCategory(ParsingUtility.HIDE_ANIMATION_PREFIX, database == null ? null : database.hideAnimationDatabase);

            // Reset tagcollection & cachedcollection
            tags = new();
            var roCData = new ReadOnlyCollection<CharData>(Mediator.CharData);
            basic = new CachedCollection<CachedAnimation>(new AnimationCacher(basicCategory, context, roCData, (x) => !IsExcludedBasic(x)), tags.AddKey(basicCategory));
            show = new CachedCollection<CachedAnimation>(new AnimationCacher(showCategory, context, roCData, (x) => !IsExcludedShow(x)), tags.AddKey(showCategory));
            hide = new CachedCollection<CachedAnimation>(new AnimationCacher(hideCategory, context, roCData, (x) => !IsExcludedHide(x)), tags.AddKey(hideCategory));

            // Reset processors
            processors ??= new();
            processors.UnregisterFrom(Mediator.Processor);
            processors.Clear();

            processors.AddProcessor(basicCategory.Prefix, new TagProcessor(basicCategory));
            processors.AddProcessor(showCategory.Prefix, new TagProcessor(showCategory));
            processors.AddProcessor(hideCategory.Prefix, new TagProcessor(hideCategory));

            processors.RegisterTo(Mediator.Processor);

            SetDefault(TMPAnimationType.Show);
            SetDefault(TMPAnimationType.Hide);
        }

        private void SetDefault(TMPAnimationType type)
        {
            string str; 

            switch (type)
            {
                case TMPAnimationType.Show:
                    defaultShow = dummyShow;
                    str = defaultShowString;
                    break;

                case TMPAnimationType.Hide:
                    defaultHide = dummyHide;
                    str = defaultHideString;
                    break;

                default:
                    throw new System.ArgumentException(nameof(type));
            }

            ParsingUtility.TagInfo tagInfo = new ParsingUtility.TagInfo();
            Dictionary<string, string> tagParams;
            ITMPAnimation animation;

            if (string.IsNullOrWhiteSpace(str)) return;
            str = (str.Trim()[0] == '<' ? str : "<" + str + ">");
            if (!ParsingUtility.TryParseTag(str, 0, str.Length - 1, ref tagInfo, ParsingUtility.TagType.Open) || !database.ContainsEffect(tagInfo.name)) return;

            if ((animation = database.GetEffect(tagInfo.name, type)) == null)
            {
                return;
            }

            tagParams = ParsingUtility.GetTagParametersDict(str);
            if (!animation.ValidateParameters(tagParams)) return;

            AnimationCacher cacher;
            switch (type)
            {
                case TMPAnimationType.Show:
                    cacher = new AnimationCacher(database?.showAnimationDatabase, context, new ReadOnlyCollection<CharData>(Mediator.CharData), x => !IsExcludedShow(x));
                    defaultShow = cacher.CacheTag(new EffectTag(tagInfo.name, tagInfo.prefix, tagParams), new EffectTagIndices());
                    break;

                case TMPAnimationType.Hide:
                    cacher = new AnimationCacher(database?.hideAnimationDatabase, context, new ReadOnlyCollection<CharData>(Mediator.CharData), x => !IsExcludedHide(x));
                    defaultHide = cacher.CacheTag(new EffectTag(tagInfo.name, tagInfo.prefix, tagParams), new EffectTagIndices());
                    break;
            }
        }

        private void OnDatabaseChanged()
        {
            PrepareForProcessing();
        }

        private void SetDummyShow()
        {
            if (dummyShow != null) return;
            EffectTag tag = new EffectTag("Dummy Show Animation", ' ', null);
            var cached = new CachedAnimation(tag, new EffectTagIndices(), ScriptableObject.CreateInstance<DummyShowAnimation>());
            dummyShow = cached;
        }

        private void SetDummyHide()
        {
            if (dummyHide != null) return;
            EffectTag tag = new EffectTag("Dummy Hide Animation", ' ', null);
            var cached = new CachedAnimation(tag, new EffectTagIndices(), ScriptableObject.CreateInstance<DummyHideAnimation>());
            dummyHide = cached;
        }
        #endregion

        #region Animation Controlling
        /// <summary>
        /// Update the current animations.
        /// </summary>
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
            OnDatabaseChanged();
            Mediator.ForceReprocess();
        }

        public void SetDefaultShowString(string str)
        {
            defaultShowString = str;
            SetDefault(TMPAnimationType.Show);
        }

        public void SetDefaultHideString(string str)
        {
            defaultHideString = str;
            SetDefault(TMPAnimationType.Hide);
        }

        public void SetExcludedCharacters(string str, bool? excludePunctuation = null)
        {
            excludedCharacters = str;
            if (excludePunctuation != null)
            {
                this.excludePunctuation = excludePunctuation.Value;
            }

            RecalculateSegmentData(hide, x => !IsExcludedBasic(x));
        }

        public void SetExcludedShowCharacters(string str, bool? excludePunctuation = null)
        {
            excludedCharactersShow = str;
            if (excludePunctuation != null)
            {
                excludePunctuationShow = excludePunctuation.Value;
            }

            RecalculateSegmentData(hide, x => !IsExcludedShow(x));
        }

        public void SetExcludedHideCharacters(string str, bool? excludePunctuation = null)
        {
            excludedCharactersHide = str;
            if (excludePunctuation != null)
            {
                excludePunctuationHide = excludePunctuation.Value;
            }

            RecalculateSegmentData(hide, x => !IsExcludedHide(x));
        }
        #endregion

        #region Animations 
        private void Update()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            if (updateFrom == UpdateFrom.Update && isAnimating) UpdateAnimations_Impl(context.useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime);
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            if (updateFrom == UpdateFrom.LateUpdate && isAnimating) UpdateAnimations_Impl(context.useScaledTime ? Time.deltaTime : Time.unscaledDeltaTime);
        }

        private void FixedUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return;
#endif
            if (updateFrom == UpdateFrom.FixedUpdate && isAnimating) UpdateAnimations_Impl(context.useScaledTime ? Time.fixedDeltaTime : Time.fixedUnscaledDeltaTime);
        }

        [System.NonSerialized] System.Diagnostics.Stopwatch sw = null;
        [System.NonSerialized] int count = 0;

        private void UpdateAnimations_Impl(float deltaTime)
        {
            if (sw == null) sw = new();
            else if (count == 50000)
            {
                Debug.Log("MEasurement aftert 50000 iterations: " + sw.Elapsed.TotalMilliseconds);
            }
            else if (count % 100 == 0) Debug.Log(count); 
            count++;
            sw.Start();

            context.passedTime += deltaTime;

            for (int i = 0; i < Mediator.CharData.Count; i++)
            {
                CharData cData = Mediator.CharData[i];
                UpdateCharacterAnimation(ref cData, deltaTime, i, false);
            }

            if (Mediator.Text.mesh != null)
                Mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);

            sw.Stop();
        }

        private void UpdateCharacterAnimation(ref CharData cData, float deltaTime, int index, bool updateVertices = true)
        {
            if (!AnimateCharacter(index, ref cData)) return;

            context.deltaTime = deltaTime;

            UpdateCharacterAnimation_Impl(index);

            // TODO only set actually changed meshes; dirty flag on cdata & cehcking of uv vert color
            var info = Mediator.Text.textInfo;
            TMP_CharacterInfo cInfo = info.characterInfo[index];
            int vIndex = cInfo.vertexIndex, mIndex = cInfo.materialReferenceIndex;
            Color32[] colors = info.meshInfo[mIndex].colors32;
            Vector3[] verts = info.meshInfo[mIndex].vertices;
            Vector2[] uvs0 = info.meshInfo[mIndex].uvs0;
            Vector2[] uvs2 = info.meshInfo[mIndex].uvs2;
            //Vector2[] uvs4 = info.meshInfo[mIndex].uvs2;

            for (int j = 0; j < 4; j++)
            {
                verts[vIndex + j] = Mediator.CharData[index].mesh[j].position;
                colors[vIndex + j] = Mediator.CharData[index].mesh[j].color;
                uvs0[vIndex + j] = Mediator.CharData[index].mesh[j].uv;
                uvs2[vIndex + j] = Mediator.CharData[index].mesh[j].uv2;
                //uvs4[vIndex + j] = mediator.CharData[index].currentMesh[j].uv4;
            }

            if (updateVertices && Mediator.Text.mesh != null)
                Mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
        }

        private bool AnimateCharacter(int index, ref CharData cData)
        {
            return cData.info.isVisible &&
                cData.visibilityState != CharData.VisibilityState.Hidden &&
                (basic.HasAnyContaining(index) ||
                (cData.visibilityState == CharData.VisibilityState.ShowAnimation && show.HasAnyContaining(index)) ||
                (cData.visibilityState == CharData.VisibilityState.HideAnimation && hide.HasAnyContaining(index)));
        }

        private int UpdateCharacterAnimation_Impl(int index)
        {
            CharData cData = Mediator.CharData[index];
            if (!cData.info.isVisible || cData.visibilityState == CharData.VisibilityState.Hidden) return 0;

            int applied = 0;

            Vector3 positionDelta = Vector3.zero;
            Matrix4x4 scaleDelta = Matrix4x4.Scale(Vector3.one);
            Quaternion rotation = Quaternion.identity;
            Vector3 rotationPivot = cData.info.initialPosition;

            Vector3 TL = Vector3.zero;//Data.mesh.initial.vertex_TL.position;
            Vector3 TR = Vector3.zero;//Data.mesh.initial.vertex_TR.position;
            Vector3 BR = Vector3.zero;//Data.mesh.initial.vertex_BR.position;
            Vector3 BL = Vector3.zero;//cData.mesh.initial.vertex_BL.position;

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
            ApplyVertices();
            return applied;

            void Animate(CachedAnimation ca)
            {
                cData.Reset();
                cData.segmentIndex = index - ca.Indices.StartIndex;

                for (int i = 0; i < 4; i++)
                {
                    cData.SetVertex(i, cData.mesh.initial.GetPosition(i)); // cData.initialMesh.GetPosition(i));
                }

                ca.animation.ResetParameters();
                ca.animation.SetParameters(ca.Tag.Parameters);
                ca.animation.Animate(ref cData, ca.context);

                UpdateVertexOffsets();

                applied++;
            }

            void AnimateList(TMPAnimationType type)
            {
                CachedCollection<CachedAnimation> cc = basic;
                switch (type)
                {
                    case TMPAnimationType.Show:
                        if (show.Count == 0)
                        {
                            Animate(defaultShow);
                            return;
                        }
                        cc = show;
                        break;

                    case TMPAnimationType.Hide:
                        if (hide.Count == 0)
                        {  
                            Animate(defaultHide);
                            return;
                        }
                        cc = hide;
                        break; 
                }

                if (animationsOverride)
                {
                    foreach (CachedAnimation animation in cc.GetContaining(index))
                    {
                        Animate(animation);
                        if (!(animation.overrides != null && !animation.overrides.Value))
                            break;
                    }
                }
                else
                {
                    foreach (CachedAnimation animation in cc.GetContaining(index))
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
                // TODO Calculation of correct values needs some work

                // Apply vertex transformations
                Vector3 vtl = cData.mesh.initial.vertex_TL.position + TL;// * (context.scaleAnimations ? cData.info.referenceScale : 1);
                Vector3 vtr = cData.mesh.initial.vertex_TR.position + TR;// * (context.scaleAnimations ? cData.info.referenceScale : 1);
                Vector3 vbr = cData.mesh.initial.vertex_BR.position + BR;// * (context.scaleAnimations ? cData.info.referenceScale : 1);
                Vector3 vbl = cData.mesh.initial.vertex_BL.position + BL;// * (context.scaleAnimations ? cData.info.referenceScale : 1);

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


                cData.SetVertex(0, vbl);
                cData.SetVertex(1, vtl);
                cData.SetVertex(2, vtr);
                cData.SetVertex(3, vbr);


                //cData.mesh.SetPosition(0, vbl);
                //cData.mesh.SetPosition(1, vtl);
                //cData.mesh.SetPosition(2, vtr);
                //cData.mesh.SetPosition(3, vbr);

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

                Mediator.CharData[index] = cData;
            }
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
                CharData cData = Mediator.CharData[start + i];
                UpdateCharacterAnimation(ref cData, 0f, start + i);
            }
        }

        private void EnsureCorrectTiming(int index, CharData.VisibilityState prev)
        {
            if (context == null) return;
            float passed = context.passedTime;
            CharData cData = Mediator.CharData[index];
            CharData.VisibilityState current = cData.visibilityState;
            cData.SetVisibilityState(prev, -1);
            cData.SetVisibilityState(current, passed);
            Mediator.CharData[index] = cData;
        }

        private void PostProcessTags()
        {
            tags.Clear();

            foreach (var processor in processors.TagProcessors[basicCategory.Prefix])
            {
                foreach (var tag in processor.ProcessedTags)
                {
                    tags[basicCategory].TryAdd(tag.Value, tag.Key);
                }
            }

            foreach (var processor in processors.TagProcessors[showCategory.Prefix])
            {
                foreach (var tag in processor.ProcessedTags)
                    tags[showCategory].TryAdd(tag.Value, tag.Key);
            }

            foreach (var processor in processors.TagProcessors[hideCategory.Prefix])
            {
                foreach (var tag in processor.ProcessedTags)
                    tags[hideCategory].TryAdd(tag.Value, tag.Key);
            }
        }
        #endregion

        #region Utility
        private void ResetAllVisible()
        {
            var info = Mediator.Text.textInfo;

            Vector3[] verts;
            Color32[] colors;
            int vIndex, mIndex;
            TMP_CharacterInfo cInfo;

            // Iterate over all characters and apply the new meshes
            for (int i = 0; i < Mediator.CharData.Count; i++)
            {
                cInfo = info.characterInfo[i];
                if (!cInfo.isVisible || Mediator.CharData[i].visibilityState == CharData.VisibilityState.Hidden) continue;

                vIndex = cInfo.vertexIndex;
                mIndex = cInfo.materialReferenceIndex;

                colors = info.meshInfo[mIndex].colors32;
                verts = info.meshInfo[mIndex].vertices;

                CharData cData = Mediator.CharData[i];

                for (int j = 0; j < 4; j++)
                {
                    cData.SetVertex(j, cData.mesh.initial.GetPosition(j));
                    cData.mesh.SetColor(j, cData.mesh.initial.GetColor(j));

                    verts[vIndex + j] = Mediator.CharData[i].mesh.initial[j].position;
                    colors[vIndex + j] = Mediator.CharData[i].mesh.initial[j].color;
                }

                Mediator.CharData[i] = cData;
            }

            if (Mediator.Text.mesh != null)
                Mediator.Text.UpdateVertexData(TMP_VertexDataUpdateFlags.All);
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

        private void RecalculateSegmentData(CachedCollection<CachedAnimation> coll, Predicate<char> animates)
        {
            ReadOnlyCollection<CharData> ro = new ReadOnlyCollection<CharData>(Mediator.CharData);
            foreach (var animation in coll)
            {
                animation.context.segmentData = new SegmentData(animation.Indices, ro, animates);
            }
        }
        #endregion

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

        [MenuItem("CONTEXT/TMP_Text/Add animator")]
        static void AddAnimator(MenuCommand command)
        {
            TMP_Text text = command.context as TMP_Text;
            if (text == null)
            {
                Debug.LogWarning("Could not add animator to " + command.context.name);
                return;
            }

            text.gameObject.GetOrAddComponent<TMPAnimator>();
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
            if (Mediator != null) Mediator.ForceReprocess();
        }

        public void ForcePostProcess()
        {
            if (Mediator != null) PostProcessTags();
        }

        public void UpdateProcessorsWrapper()
        {
            if (Mediator == null) return;
            OnDatabaseChanged();
        }

        public string CheckDefaultShowString()
        {
            if (database == null || database.showAnimationDatabase == null) return "No show database assigned on animation database";

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
            if (database == null || database.hideAnimationDatabase == null) return "No hide database assigned on animation database";

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
            if (MediatorThreadSafe == null) return;

            if (prevDatabase != database || (database != null && (basicCategory.Database != (ITMPEffectDatabase<ITMPAnimation>)database.basicAnimationDatabase || showCategory.Database != (ITMPEffectDatabase<ITMPAnimation>)database.showAnimationDatabase || hideCategory.Database != (ITMPEffectDatabase<ITMPAnimation>)database.hideAnimationDatabase)))
            {
                prevDatabase = database;
                OnDatabaseChanged();
            }
        }
#endif
        #endregion
    }










    internal class AnimationCacher : ITagCacher<CachedAnimation>
    {
        private ITMPEffectDatabase<ITMPAnimation> database;
        private IList<CharData> charData;
        private AnimatorContext context;
        private Predicate<char> animates;

        public AnimationCacher(ITMPEffectDatabase<ITMPAnimation> database, AnimatorContext context, IList<CharData> charData, Predicate<char> animates)
        {
            this.context = context;
            this.database = database;
            this.charData = charData;
            this.animates = animates;
        }

        public CachedAnimation CacheTag(EffectTag tag, EffectTagIndices indices)
        {
            CachedAnimation ca = new CachedAnimation(tag, new EffectTagIndices(indices.StartIndex, indices.IsOpen ? charData.Count : indices.EndIndex, indices.OrderAtIndex), database.GetEffect(tag.Name));
            ca.context.animatorContext = context;
            ca.context.segmentData = new SegmentData(ca.Indices, charData, animates);
            return ca;
        }
    }

    public class DummyShowAnimation : TMPShowAnimation
    {
        public override void Animate(ref CharData cData, IAnimationContext context)
        {
            for (int i = 0; i < 4; i++)
            {
                cData.SetVertex(i, cData.mesh.initial.GetPosition(i));
            }
        }

        public override void ResetParameters()
        {
        }

        public override void SetParameters(IDictionary<string, string> parameters)
        {
        }

        public override bool ValidateParameters(IDictionary<string, string> parameters)
        {
            return true;
        }
    }

    public class DummyHideAnimation : TMPShowAnimation
    {
        public override void Animate(ref CharData cData, IAnimationContext context)
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

        public override void SetParameters(IDictionary<string, string> parameters)
        {
        }

        public override bool ValidateParameters(IDictionary<string, string> parameters)
        {
            return true;
        }
    }
}