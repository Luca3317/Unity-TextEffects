using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TMPEffects.Databases
{
    public interface ITMPEffectDatabase
    {
        public bool ContainsEffect(string name);
    }

    public interface ITMPEffectDatabase<T> : ITMPEffectDatabase
    {
        public T GetEffect(string name);
    }
}