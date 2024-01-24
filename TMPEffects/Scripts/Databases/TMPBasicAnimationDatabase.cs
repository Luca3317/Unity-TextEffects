using AYellowpaper.SerializedCollections;
using UnityEngine;
using TMPEffects.Animations;

namespace TMPEffects.Databases
{
    /// <summary>
    /// Stores <see cref="TMPAnimation"/> animations.
    /// </summary>
    [CreateAssetMenu(fileName = "new TMPBasicAnimationDatabase", menuName = "TMPEffects/Database/Basic Animation Database", order = 11)]
    public class TMPBasicAnimationDatabase : TMPAnimationDatabaseBase<TMPAnimation>
    {
        [SerializedDictionary(keyName: "Tag Name", valueName: "Animation")]
        [SerializeField] SerializedDictionary<string, TMPAnimation> animations;

        public override bool ContainsEffect(string name)
        {
            return animations.ContainsKey(name);
        }

        public override TMPAnimation GetEffect(string name)
        {
            return animations[name];
        }
    }
}
