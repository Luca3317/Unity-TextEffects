using System.Collections.Generic;
using TMPEffects;
using TMPEffects.Tags;
using TMPEffects.TextProcessing;

// TODO Should be sealed?
public sealed class TagProcessor
{
    private List<EffectTag> tags;
    private ITMPTagValidator validator;

    public IEnumerable<EffectTag> ProcessedTags
    {
        get
        {
            foreach (EffectTag tag in tags) yield return tag;
        }
    }

    public TagProcessor(ITMPTagValidator validator)
    {
        tags = new();
        this.validator = validator;
    }

    public bool Process(ParsingUtility.TagInfo tagInfo, int textIndex, int orderAtIndex)
    {
        if (tagInfo.type == ParsingUtility.TagType.Open) return Process_Open(tagInfo, textIndex, orderAtIndex);
        else return Process_Close(tagInfo, textIndex);
    }

    private bool Process_Open(ParsingUtility.TagInfo tagInfo, int textIndex, int orderAtIndex)
    {
        EffectTagData data;
        if (!validator.ValidateTag(tagInfo, out data)) return false;

        EffectTag tag = new EffectTag(data, new EffectTagIndices(textIndex, -1, orderAtIndex));
        tags.Add(tag);

        return true;
    }

    private bool Process_Close(ParsingUtility.TagInfo tagInfo, int textIndex)
    {
        if (!validator.ValidateTag(tagInfo)) return false;   

        for (int i = tags.Count - 1; i >= 0; i--)
        {
            if (tags[i].IsOpen && tags[i].Name == tagInfo.name)
            {
                tags[i].SetEndIndex(textIndex);
                return true;
            }
        }

        return true;
    }

    public void Reset()
    {
        tags.Clear();
    }
}