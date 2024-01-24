using System.Collections.Generic;
using TMPro;
using UnityEngine;
using TMPEffects.TextProcessing;
using TMPEffects.Extensions;

namespace TMPEffects.Components
{
    /*
     * A mediator class for TMPAnimator and TMPWriter (and potential additions, if any).
     * Handles the pre- and postprocessing of the text, as well as maintaining information
     * about it in the form of a CharData collection.
     */
    [DisallowMultipleComponent]
    public class TMPMediator : MonoBehaviour
    {

        public void VisibilityStateUpdated(int index, CharData.VisibilityState previous)
        {
            OnVisibilityStateUpdated?.Invoke(index, previous);
        }

        public delegate void VisibilityEventHandler(int index, CharData.VisibilityState previous);
        public event VisibilityEventHandler OnVisibilityStateUpdated;



        public bool isInitialized => initialized;

        //public int Subscribers { get; private set; }
        public List<CharData> CharData { get; private set; }
        internal TMPTextProcessor Processor { get; private set; }
        public TMP_Text Text { get; private set; }

        public delegate void EmptyEventHandler();
        public delegate void RangeEventHandler(int start, int lenght);
        public event EmptyEventHandler TextChanged;
        public event RangeEventHandler ForcedUpdate;
        public event EmptyEventHandler CharDataPopulated;

        [System.NonSerialized] private bool initialized = false;

        public void Initialize()
        {
            if (initialized) return;

            initialized = true;

            subscribers = new List<object>();
            Text = GetComponent<TMP_Text>();
            CharData = new List<CharData>();
            Processor = new TMPTextProcessor(Text);

            SetPreprocessor();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        }

        public void ForceReprocess()
        {
            Text.ForceMeshUpdate(true, true);
        }

        public void ForceUpdate(int start, int length)
        {
            ForcedUpdate?.Invoke(start, length);
        }

        void OnTextChanged(Object obj)
        {
            if ((obj as TMP_Text) == Text)
            {
                TextChangedProcedure();
            }
        }

        void TextChangedProcedure()
        {
            Processor.AdjustIndices(Text.textInfo);
            PopulateCharData();
            TextChanged?.Invoke();
        }

        void SetPreprocessor()
        {
            Text.textPreprocessor = Processor;
        }

        void UnsetPreprocessor()
        {
            if (Text.textPreprocessor == Processor)
                Text.textPreprocessor = null;
        }

        void PopulateCharData()
        {
            CharData.Clear();

            TMP_TextInfo info = Text.textInfo;
            CharData data;
            TMP_WordInfo? wordInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var cInfo = info.characterInfo[i];
                wordInfo = null;

                if (cInfo.isVisible)
                {
                    for (int j = 0; j < info.wordCount; j++)
                    {
                        wordInfo = info.wordInfo[j];
                        if (wordInfo.Value.firstCharacterIndex <= i && wordInfo.Value.lastCharacterIndex >= i)
                        {
                            break;
                        }
                    }
                }

                data = wordInfo == null ? new CharData(i, cInfo) : new CharData(i, cInfo, wordInfo.Value);
                CharData.Add(data);
            }

            CharDataPopulated?.Invoke();
        }

        List<object> subscribers = new List<object>();

        public void Subscribe(object obj)
        {
            if (subscribers.Contains(obj)) return;
            subscribers.Add(obj);
        }

        public void Unsubscribe(object obj)
        {
            if (!subscribers.Contains(obj)) return;
            subscribers.Remove(obj);

            if (subscribers.Count == 0)
            {
                // This check is meant to prevent doubly calling destroy
                // on this component if the gameobject is destroyed
                // (it being destroyed will cause the other objects to call Unsubscribe)

                if (!destroyCancellationToken.IsCancellationRequested && gameObject.activeInHierarchy)
                {
                    UnsetPreprocessor();
                    TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

#if UNITY_EDITOR
                    if (Application.isPlaying) Destroy(this);
                    else DestroyImmediate(this);
#else
                Destroy(this);
#endif
                }

                //                if (gameObject.activeInHierarchy)
                //                {
                //                    UnsetPreprocessor();
                //                    TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

                //#if UNITY_EDITOR
                //                    if (Application.isPlaying) Destroy(this);
                //                    else DestroyImmediate(this);
                //#else
                //                Destroy(this);
                //#endif
                //}
            }
        }

        public static TMPMediator Create(GameObject go)
        {
            TMPMediator tmf = go.GetOrAddComponent<TMPMediator>();
            tmf.hideFlags = HideFlags.HideInInspector;
            tmf.Initialize();
            return tmf;
        }
    }
}