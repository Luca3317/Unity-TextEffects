using System.Collections.Generic;
using TMPEffects;
using TMPEffects.Tags;
using TMPEffects.TextProcessing;

// TODO Should be sealed?
public sealed class TagProcessor
{
    public readonly List<KeyValuePair<EffectTagIndices, EffectTag>> ProcessedTags;
    private ITMPTagValidator validator;

    public TagProcessor(ITMPTagValidator validator)
    {
        ProcessedTags = new();
        this.validator = validator;
    }

    public bool Process(ParsingUtility.TagInfo tagInfo, int textIndex, int orderAtIndex)
    {
        if (tagInfo.type == ParsingUtility.TagType.Open) return Process_Open(tagInfo, textIndex, orderAtIndex);
        else return Process_Close(tagInfo, textIndex);
    }

    private bool Process_Open(ParsingUtility.TagInfo tagInfo, int textIndex, int orderAtIndex)
    {
        EffectTag tag;
        if (!validator.ValidateTag(tagInfo, out tag)) return false;

        EffectTagIndices indices = new EffectTagIndices(textIndex, -1, orderAtIndex);
        KeyValuePair<EffectTagIndices, EffectTag> kvp = new KeyValuePair<EffectTagIndices, EffectTag>(indices, tag);
        ProcessedTags.Add(kvp);

        return true;
    }

    private bool Process_Close(ParsingUtility.TagInfo tagInfo, int textIndex)
    {
        if (!validator.ValidateTag(tagInfo)) return false;

        KeyValuePair<EffectTagIndices, EffectTag> kvp;
        for (int i = ProcessedTags.Count - 1; i >= 0; i--)
        {
            kvp = ProcessedTags[i];
            if (kvp.Key.IsOpen && kvp.Value.Name == tagInfo.name)
            {
                EffectTagIndices newIndices = new EffectTagIndices(kvp.Key.StartIndex, textIndex, kvp.Key.OrderAtIndex);
                ProcessedTags[i] = new KeyValuePair<EffectTagIndices, EffectTag>(newIndices, kvp.Value);
                return true;
            }
        }

        return true;
    }

    internal void AdjustIndices(KeyValuePair<EffectTagIndices, EffectTag> oldPair, KeyValuePair<EffectTagIndices, EffectTag> newPair)
    {
        int index = ProcessedTags.IndexOf(oldPair);
        if (index < 0) return;

        ProcessedTags[index] = newPair;
    }

    public void Reset()
    {
        ProcessedTags.Clear();
    }
}