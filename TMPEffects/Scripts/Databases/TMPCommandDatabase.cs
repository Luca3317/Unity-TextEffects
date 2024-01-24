using AYellowpaper.SerializedCollections;
using UnityEngine;

namespace TMPEffects.Databases
{
    /// <summary>
    /// Stores <see cref="TMPCommand"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "new TMPCommandDatabase", menuName = "TMPEffects/Database/Command Database", order = 30)]
    public class TMPCommandDatabase : TMPEffectDatabase<TMPCommand>
    {
        [SerializedDictionary(keyName: "Tag Name", valueName: "Command")]
        [SerializeField] SerializedDictionary<string, TMPCommand> commandDict;

        public override bool ContainsEffect(string name)
        {
            return commandDict.ContainsKey(name);
        }

        public override TMPCommand GetEffect(string name)
        {
            return commandDict[name];
        }
    }
}
