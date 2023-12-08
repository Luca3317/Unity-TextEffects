using System.Collections.Generic;
using TMPEffects.Databases;
using TMPEffects.Tags;
using static TMPEffects.TextProcessing.ParsingUtility;

namespace TMPEffects.TextProcessing.TagProcessors
{
    public class AnimationTagProcessor<T> : ITagProcessor<TMPAnimationTag> where T : TMPAnimation
    {
        public TMPAnimationDatabaseBase<T> Database => database;

        public List<TMPAnimationTag> ProcessedTags
        {
            get; private set;
        }

        TMPAnimationDatabaseBase<T> database;

        public AnimationTagProcessor(TMPAnimationDatabaseBase<T> database)
        {
            this.database = database;
            ProcessedTags = new();
        }

        public bool PreProcess(TagInfo tagInfo)
        {
            // TODO How to handle this case?
            // Technically you probably shouldnt be able to create a
            // processor with an invalid database
            // DO SAME FOR COMMANDTAGPROCESSOR
            if (database == null) return false;

            // check name
            if (!database.Contains(tagInfo.name)) return false;

            TMPAnimation animation;
            if ((animation = database.GetEffect(tagInfo.name)) == null) return false;

            if (tagInfo.type == TagType.Open)
            {
                // check parameters
                var parameters = GetTagParametersDict(tagInfo.parameterString, 0);
                if (!animation.ValidateParameters(parameters)) return false;
            }
            return true;
        }

        public bool Process(TagInfo tagInfo, int textIndex)
        {
            // TODO How to handle this case?
            // Technically you probably shouldnt be able to create a
            // processor with an invalid database
            // DO SAME FOR COMMANDTAGPROCESSOR
            if (database == null) return false;

            // check name
            if (!database.Contains(tagInfo.name)) return false;

            TMPAnimation animation;
            if ((animation = database.GetEffect(tagInfo.name)) == null) return false;

            TMPAnimationTag tag;
            if (tagInfo.type == TagType.Open)
            {
                // check parameters
                var parameters = GetTagParametersDict(tagInfo.parameterString, 0);
                if (!animation.ValidateParameters(parameters)) return false;

                tag = new TMPAnimationTag(tagInfo.name, textIndex, parameters);
                ProcessedTags.Add(tag);
            }
            else
            {
                for (int i = ProcessedTags.Count - 1; i >= 0; i--)
                {
                    tag = ProcessedTags[i];

                    if (tag.IsOpen && tag.IsEqual(tagInfo.name))
                    {
                        tag.Close(textIndex - 1);
                        break;
                    }
                }
            }

            return true;
        }

        public void Reset()
        {
            ProcessedTags.Clear();
        }
    }
}
