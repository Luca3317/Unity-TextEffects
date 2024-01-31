//using System;
//using TMPEffects.TextProcessing;
//using UnityEngine;

//namespace TMPEffects.Components
//{
//    /*
//     * Some major questions left to decide:
//     * 
//     *      - Giving it a reference to mediator seems massively wrong; can do with ReadOnlyCollection<CharData> and TMP_Text?
//     *              Maybe even just some CharDataCollection wrapper with a ApplyChanges method
//     *              
//     *      - How exactly to split settings variables such as animationsOverride, excludePunctuation etc
//     *              Either
//     *                  Have them as serialized fields on here and System.Serializable the class
//     *              or
//     *                  Accept a settings argument in the constructor
//     * 
//     */
//    [System.Serializable]
//    public class TMPAnimator : ITMPAnimator
//    {

//        [SerializeField] private bool animationsOverride = true;

//        [SerializeField] private string excludedCharacters = "";
//        [SerializeField] private string excludedCharactersShow = "";
//        [SerializeField] private string excludedCharactersHide = "";

//        [SerializeField] private bool excludePunctuation = false;
//        [SerializeField] private bool excludePunctuationShow = false;
//        [SerializeField] private bool excludePunctuationHide = false;

//        [System.NonSerialized] private CachedCollection<CachedAnimation> basic;
//        [System.NonSerialized] private CachedCollection<CachedAnimation> show;
//        [System.NonSerialized] private CachedCollection<CachedAnimation> hide;

//        public TMPAnimator( ITagManager<TMPAnimationCategory> tags, ITagProcessorManager processors  )
//        {
//            tags.AddKey(new TMPAnimationCategory(ParsingUtility.NO_PREFIX, ))


//        }


//        public void ResetAnimations()
//        {
//            throw new System.NotImplementedException();
//        }

//        public void StartAnimating()
//        {
//            throw new System.NotImplementedException();
//        }

//        public void StopAnimating()
//        {
//            throw new System.NotImplementedException();
//        }

//        public void UpdateAnimations(float deltaTime)
//        {
//            throw new System.NotImplementedException();
//        }
//    }
//}