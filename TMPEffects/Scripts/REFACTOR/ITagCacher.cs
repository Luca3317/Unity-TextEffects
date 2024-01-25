using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TMPEffects.Tags
{
    public interface ITagCacher<T> where T : ITagWrapper
    {
        public bool TryCache(EffectTag tag, out T cached);
    }
}