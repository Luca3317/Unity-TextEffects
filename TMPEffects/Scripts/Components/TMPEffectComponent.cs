using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TMPEffects.Extensions;
using TMPEffects.Tags;
using TMPEffects.TextProcessing;
using TMPro;
using UnityEngine;

namespace TMPEffects.Components
{
    /// <summary>
    /// Base class for TMPAnimator and TMPWriter.
    /// </summary>
    public abstract class TMPEffectComponent : MonoBehaviour
    {
        public TMP_Text TextComponent => Mediator.Text;
        public TMP_TextInfo TextInfo => Mediator.Text.textInfo;

        [System.NonSerialized] private readonly object obj = new();
        [System.NonSerialized] private TMPMediator mediator = null;

        protected TMPMediator Mediator
        {
            get
            {
                if (mediator == null)
                {
                    if (!TMPMediatorManager.TryGetMediator(gameObject, out mediator))
                    {
                        TMPMediatorManager.Subscribe(GetComponent<TMP_Text>(), obj);
                        mediator = TMPMediatorManager.GetMediator(gameObject);
                    }
                }
                return mediator;
            }
        }

        protected TMPMediator MediatorThreadSafe
        {
            get
            {
                return mediator;
            }
        }

        protected void FreeMediator() 
        {
            TMPMediatorManager.Unsubscribe(GetComponent<TMP_Text>(), obj);
            mediator = null;
        }

        public int CharacterCount => Mediator.CharData.Count;

        public void SetText(string text)
        {
            Mediator.Text.SetText(text);
        }

        public void UpdateMeshes(TMPro.TMP_VertexDataUpdateFlags flags = TMPro.TMP_VertexDataUpdateFlags.All)
        {
            Mediator.Text.UpdateVertexData(flags);
        }

        protected void UpdateMediator()
        {
            //GetMediator = TMPMediator.Create(gameObject);
        }
    }
}

