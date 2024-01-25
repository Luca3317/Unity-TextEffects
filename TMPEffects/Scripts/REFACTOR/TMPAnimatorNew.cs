using TMPEffects.Tags;
using TMPEffects.TextProcessing;
using TMPEffects;
using TMPEffects.Databases;
using System.Collections.Generic;
using System.Collections;

internal class CachedAnimation : ITagWrapper
{
    public EffectTag Tag => tag;

    public bool? overrides;
    public ITMPAnimation animation;
    public IAnimationContext context;

    private EffectTag tag;

    public CachedAnimation(EffectTag tag, ITMPAnimation animation)
    {
        this.tag = tag;
        overrides = null;
        if (tag.Parameters != null)
        {
            bool tmp;
            foreach (var param in tag.Parameters.Keys)
            {
                switch (param)
                {
                    case "override":
                    case "or":
                        if (ParsingUtility.StringToBool(tag.Parameters[param], out tmp)) overrides = tmp;
                        // TODO remove it from parameters?
                        break;
                }
            }
        }

        this.animation = animation;
        this.context = animation.GetNewContext();

        // TODO these should be lazily loaded
        //if (context != null) context.animatorContext = animatorContext;
        //context.segmentData = new SegmentData(animator, tag, mediator.CharData);
    }
}



public interface ITMPAnimator
{
    /// <summary>
    /// Update the current animations.
    /// </summary>
    /// TODO Enforce calling StartAnimating when UpdateFrom.Script?
    /// TODO Allow calling this when not updating from Script?
    public void UpdateAnimations(float deltaTime);

    /// <summary>
    /// Start animating.
    /// </summary>
    public void StartAnimating();

    /// <summary>
    /// Stop animating.
    /// </summary>
    public void StopAnimating();

    /// <summary>
    /// Reset all visible characters to their initial state.
    /// </summary>
    public void ResetAnimations();

    /// <summary>
    /// Set where the animations should be updated from.
    /// </summary>
    /// <param name="updateFrom"></param>
    public void SetUpdateFrom(UpdateFrom updateFrom);

    /// <summary>
    /// Set the database the animator should use to parse the text's animation tags.
    /// </summary>
    /// <param name="database"></param>
    public void SetDatabase(TMPAnimationDatabase database);
}


public class TMPAnimatorNew : ITMPAnimator
{
    public ITagManager<TMPAnimationCategory> Tags => tags;

    private ITMPAnimator impl;
    private AnimationTagManager tags;
    private ITagProcessorManager processors;

    


    #region ITMPAnimator delegation
    public void ResetAnimations() => impl.ResetAnimations();
    public void SetDatabase(TMPAnimationDatabase database) => impl.SetDatabase(database);
    public void SetUpdateFrom(UpdateFrom updateFrom) => impl.SetUpdateFrom(updateFrom);
    public void StartAnimating() => impl.StartAnimating();
    public void StopAnimating() => impl.StopAnimating();
    public void UpdateAnimations(float deltaTime) => impl.UpdateAnimations(deltaTime);
    #endregion

    //#region ITagManager delegation
    //public int TagCount => tags.TagCount;
    //public IEnumerable<EffectTag> Tags => tags.Tags;
    //public IEnumerable<TMPAnimationCategory> Keys => tags.Keys;
    //public int KeyCount => tags.KeyCount;
    //public bool TryAdd(EffectTag tag) => tags.TryAdd(tag);
    //public int RemoveAllAt(int startIndex, EffectTag[] buffer = null, int bufferIndex = 0) => tags.RemoveAllAt(startIndex, buffer, bufferIndex);
    //public bool RemoveAt(int startIndex, int? order = null) => tags.RemoveAt(startIndex, order);
    //public void Clear() => tags.Clear();
    //public bool Contains(EffectTag item) => tags.Contains(item);
    //public void CopyTo(EffectTag[] array, int arrayIndex) => tags.CopyTo(array, arrayIndex);
    //public bool Remove(EffectTag item) => tags.Remove(item);
    //public int TagsAt(int startIndex, EffectTag[] buffer, int bufferIndex = 0) => tags.TagsAt(startIndex, buffer, bufferIndex);
    //public IEnumerable<EffectTag> TagsAt(int startIndex) => tags.TagsAt(startIndex);
    //public EffectTag TagAt(int startIndex, int? order = null) => tags.TagAt(startIndex, order);
    //public IEnumerator<EffectTag> GetEnumerator() => tags.GetEnumerator();
    //IEnumerator IEnumerable.GetEnumerator() => tags.GetEnumerator();
    //public IReadOnlyTagCollection TagsFor(TMPAnimationCategory key) => tags.TagsFor(key);
    //public bool ContainsKey(TMPAnimationCategory key) => tags.ContainsKey(key);
    //#endregion

    private class AnimationTagManager : TagManager<TMPAnimationCategory, CachedAnimation> { }
}


internal class TMPAnimator_Impl : ITMPAnimator
{
    private ICachedTagManager<TMPAnimationCategory, CachedAnimation> tags;


    public TMPAnimator_Impl(ICachedTagManager<TMPAnimationCategory, CachedAnimation> tagManager)
    {
        tags = tagManager;
    }


    #region ITMPAnimator Implementation
    public void ResetAnimations()
    {
        throw new System.NotImplementedException();
    }

    public void SetDatabase(TMPAnimationDatabase database)
    {
        throw new System.NotImplementedException();
    }

    public void SetUpdateFrom(UpdateFrom updateFrom)
    {
        throw new System.NotImplementedException();
    }

    public void StartAnimating()
    {
        throw new System.NotImplementedException();
    }

    public void StopAnimating()
    {
        throw new System.NotImplementedException();
    }

    public void UpdateAnimations(float deltaTime)
    {
        throw new System.NotImplementedException();
    }
    #endregion
}