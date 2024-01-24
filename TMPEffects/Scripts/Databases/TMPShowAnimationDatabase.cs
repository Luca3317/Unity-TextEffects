using AYellowpaper.SerializedCollections;
using UnityEngine;
using TMPEffects.Animations;

namespace TMPEffects.Databases
{
    /// <summary>
    /// Stores <see cref="TMPShowAnimation"/> animations.
    /// </summary>
    [CreateAssetMenu(fileName = "new TMPShowAnimationDatabase", menuName = "TMPEffects/Database/Show Animation Database", order = 12)]
    public class TMPShowAnimationDatabase : TMPAnimationDatabaseBase<TMPShowAnimation>
    {
        [SerializedDictionary(keyName: "Tag Name", valueName: "Show Animation")]
        [SerializeField] SerializedDictionary<string, TMPShowAnimation> showAnimations;

        public override bool ContainsEffect(string name)
        {
            return showAnimations.ContainsKey(name);
        }

        public override TMPShowAnimation GetEffect(string name)
        {
            return showAnimations[name];
        }
    }
}