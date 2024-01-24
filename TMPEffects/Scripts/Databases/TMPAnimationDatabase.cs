using System.Collections.Generic;
using UnityEngine;
using TMPEffects.Animations;

namespace TMPEffects.Databases
{
    /// <summary>
    /// Stores <see cref="TMPAnimation"/>, <see cref="TMPShowAnimation"/> and <see cref="TMPHideAnimation"/> animations.
    /// </summary>
    [CreateAssetMenu(fileName = "new TMPAnimationDatabase", menuName = "TMPEffects/Database/Animation Database", order = 0)]
    public class TMPAnimationDatabase : TMPEffectDatabase<ITMPAnimation>
    {
        public TMPBasicAnimationDatabase basicAnimationDatabase;
        public TMPShowAnimationDatabase showAnimationDatabase;
        public TMPHideAnimationDatabase hideAnimationDatabase;

        public bool Contains(string name, TMPAnimationType type)
        {
            switch (type)
            {
                case TMPAnimationType.Basic: return basicAnimationDatabase.ContainsEffect(name);
                case TMPAnimationType.Show: return showAnimationDatabase.ContainsEffect(name);
                case TMPAnimationType.Hide: return hideAnimationDatabase.ContainsEffect(name);
            }

            throw new System.ArgumentException(nameof(type));
        }

        public override bool ContainsEffect(string name)
        {
            if (basicAnimationDatabase.ContainsEffect(name)) return true;
            if (showAnimationDatabase.ContainsEffect(name)) return true;
            if (hideAnimationDatabase.ContainsEffect(name)) return true;
            return false;
        }

        public ITMPAnimation GetEffect(string name, TMPAnimationType type)
        {
            switch (type)
            {
                case TMPAnimationType.Basic: return basicAnimationDatabase.GetEffect(name);
                case TMPAnimationType.Show: return showAnimationDatabase.GetEffect(name);
                case TMPAnimationType.Hide: return hideAnimationDatabase.GetEffect(name);
            }

            throw new System.ArgumentException(nameof(type));
        }

        public override ITMPAnimation GetEffect(string name)
        {
            if (basicAnimationDatabase.ContainsEffect(name)) return basicAnimationDatabase.GetEffect(name);
            if (showAnimationDatabase.ContainsEffect(name)) return showAnimationDatabase.GetEffect(name);
            if (hideAnimationDatabase.ContainsEffect(name)) return hideAnimationDatabase.GetEffect(name);
            throw new KeyNotFoundException();
        }
    }
}