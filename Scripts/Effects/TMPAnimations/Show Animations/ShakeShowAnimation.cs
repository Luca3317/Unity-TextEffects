using System.Collections.Generic;
using TMPEffects.Components.CharacterData;
using UnityEngine;
using static TMPEffects.ParameterUtility;

namespace TMPEffects.TMPAnimations.Animations
{
    [CreateAssetMenu(fileName = "new ShakeShowAnimation", menuName = "TMPEffects/Show Animations/Shake")]
    public class ShakeShowAnimation : TMPShowAnimation
    {
        [SerializeField] float duration = 1f;
        [SerializeField] bool uniform = false;

        [SerializeField] float maxXAmplitude = 5;
        [SerializeField] float minXAmplitude = 5;
        [SerializeField] float maxYAmplitude = 5;
        [SerializeField] float minYAmplitude = 5;

        [SerializeField] bool uniformDelay = true;
        [SerializeField] float minDelay = 0.1f;
        [SerializeField] float maxDelay = 0.1f;


        public override void Animate(CharData cData, IAnimationContext context)
        {

            Data d = context.customData as Data;

            float l = Mathf.Lerp(0f, 1f, (context.animatorContext.PassedTime - context.animatorContext.StateTime(cData)) / d.duration);
            if (l == 1)
            {
                context.FinishAnimation(cData);
                return;
            }


        }



        public void AnimateA(CharData cData, IAnimationContext context)
        {
            Data d = context.customData as Data;

            if (!d.init)
            {
                d.init = true;

                if (!d.uniform)
                {
                    InitRNGDict(context);
                    InitLastUpdatedDict(context);
                    InitDelayDict(context);
                    InitOffsetDict(context);

                    if (d.uniformDelay) InitAutoUpdateDict(context);
                }
                else
                {
                    d.rng = new System.Random((int)(Time.time * 1000));
                }
            }

            float passed = context.animatorContext.PassedTime - context.animatorContext.StateTime(cData);
            float passedT = Mathf.Lerp(0, 1, passed / d.duration);


            if (passedT == 1)
            {
                context.FinishAnimation(cData);
                return;
            }

            Debug.Log("For " + context.segmentData.SegmentIndexOf(cData) + ": " + passed);

            Vector3 offset;
            if (d.uniform)
            {
                Uniform(cData, context, passed, passedT, out offset);
            }
            else if (d.uniformDelay)
            {
                UniformDelay(cData, context, passed, passedT, out offset);
            }
            else
            {
                Individual(cData, context, passed, passedT, out offset);
            }

            cData.SetPosition(cData.info.initialPosition + offset);
        }

        private void Uniform(CharData cData, IAnimationContext context, float passed, float passedT, out Vector3 offset)
        {
            Data d = context.customData as Data;

            if (context.animatorContext.PassedTime - d.lastUpdated >= d.delay)
            {
                float xAmp = d.maxXAmplitude == d.minXAmplitude ? d.maxXAmplitude : Mathf.Lerp(d.minXAmplitude, d.maxXAmplitude, (float)d.rng.NextDouble());
                float yAmp = d.maxYAmplitude == d.minYAmplitude ? d.maxYAmplitude : Mathf.Lerp(d.minYAmplitude, d.maxYAmplitude, (float)d.rng.NextDouble());

                if (d.minDelay > d.duration - passed)
                {
                    d.delay = float.MaxValue;
                    d.lastUpdated = context.animatorContext.PassedTime;
                    d.xOffset = 0f;
                    d.yOffset = 0f;
                }
                else
                {
                    d.delay = d.maxDelay == d.minDelay ? d.maxDelay : Mathf.Clamp(Mathf.Lerp(d.minDelay, d.maxDelay, (float)d.rng.NextDouble()), d.minDelay, passed);
                    d.lastUpdated = context.animatorContext.PassedTime;

                    d.xOffset = ((float)d.rng.NextDouble() * 2f - 1f) * xAmp;
                    d.yOffset = ((float)d.rng.NextDouble() * 2f - 1f) * yAmp;
                }
            }

            offset = new Vector3(d.xOffset, d.yOffset, 0f);
            if (context.segmentData.SegmentIndexOf(cData) == 0)
                Debug.Log("Offset " + offset);
        }

        private void UniformDelay(CharData cData, IAnimationContext context, float passed, float passedT, out Vector3 offset)
        {
            Data d = context.customData as Data;

            int segmentIndex = context.segmentData.SegmentIndexOf(cData);
            if (d.autoUpdateDict[segmentIndex] || context.animatorContext.PassedTime - d.sharedLastUpdated >= d.sharedDelay)
            {
                if (d.autoUpdateDict[segmentIndex]) d.autoUpdateDict[segmentIndex] = false;
                else
                {
                    if (d.minDelay > d.duration - passed)
                    {
                        d.sharedDelay = float.MaxValue;
                        d.sharedLastUpdated = context.animatorContext.PassedTime;

                        for (int i = 0; i < context.segmentData.length; i++)
                        {
                            if (i == segmentIndex) continue;
                            d.autoUpdateDict[i] = true;
                        }

                        d.offsetDict[segmentIndex] = Vector3.zero;
                    }
                    else
                    {
                        float xAmp = d.maxXAmplitude == d.minXAmplitude ? d.maxXAmplitude : Mathf.Lerp(d.minXAmplitude, d.maxXAmplitude, (float)d.rngDict[segmentIndex].NextDouble());
                        float yAmp = d.maxYAmplitude == d.minYAmplitude ? d.maxYAmplitude : Mathf.Lerp(d.minYAmplitude, d.maxYAmplitude, (float)d.rngDict[segmentIndex].NextDouble());

                        d.sharedDelay = d.maxDelay == d.minDelay ? d.maxDelay : Mathf.Clamp(Mathf.Lerp(d.minDelay, d.maxDelay, (float)d.rngDict[segmentIndex].NextDouble()), d.minDelay, passed);
                        d.sharedLastUpdated = context.animatorContext.PassedTime;

                        for (int i = 0; i < context.segmentData.length; i++)
                        {
                            if (i == segmentIndex) continue;
                            d.autoUpdateDict[i] = true;
                        }

                        float xOffset = ((float)d.rngDict[segmentIndex].NextDouble() * 2f - 1f) * xAmp;
                        float yOffset = ((float)d.rngDict[segmentIndex].NextDouble() * 2f - 1f) * yAmp;
                        d.offsetDict[segmentIndex] = new Vector3(xOffset, yOffset, 0f);
                    }
                }
            }

            offset = d.offsetDict[segmentIndex];
        }

        private void Individual(CharData cData, IAnimationContext context, float passed, float passedT, out Vector3 offset)
        {
            Data d = context.customData as Data;

            int segmentIndex = context.segmentData.SegmentIndexOf(cData);
            if (context.animatorContext.PassedTime - d.lastUpdatedDict[segmentIndex] >= d.delayDict[segmentIndex])
            {
                if (d.minDelay > d.duration - passed)
                {
                    d.delayDict[segmentIndex] = float.MaxValue;
                    d.lastUpdatedDict[segmentIndex] = context.animatorContext.PassedTime;
                    d.offsetDict[segmentIndex] = Vector3.zero;
                }
                else
                {
                    float xAmp = d.maxXAmplitude == d.minXAmplitude ? d.maxXAmplitude : Mathf.Lerp(d.minXAmplitude, d.maxXAmplitude, (float)d.rngDict[segmentIndex].NextDouble());
                    float yAmp = d.maxYAmplitude == d.minYAmplitude ? d.maxYAmplitude : Mathf.Lerp(d.minYAmplitude, d.maxYAmplitude, (float)d.rngDict[segmentIndex].NextDouble());

                    d.delayDict[segmentIndex] = d.maxDelay == d.minDelay ? d.maxDelay : Mathf.Lerp(d.minDelay, d.maxDelay, (float)d.rngDict[segmentIndex].NextDouble());
                    d.lastUpdatedDict[segmentIndex] = context.animatorContext.PassedTime;

                    float xOffset = ((float)d.rngDict[segmentIndex].NextDouble() * 2f - 1f) * xAmp;
                    float yOffset = ((float)d.rngDict[segmentIndex].NextDouble() * 2f - 1f) * yAmp;
                    d.offsetDict[segmentIndex] = new Vector3(xOffset, yOffset, 0f);
                }
            }

            offset = d.offsetDict[segmentIndex];
        }


        private void InitRNGDict(IAnimationContext context)
        {
            Data d = context.customData as Data;
            int seed = (int)(context.animatorContext.PassedTime * 1000);
            d.rngDict = new Dictionary<int, System.Random>(context.segmentData.length);
            for (int i = 0; i < context.segmentData.length; i++)
            {
                d.rngDict.Add(i, new System.Random(seed + i));
            }
        }

        private void InitLastUpdatedDict(IAnimationContext context)
        {
            Data d = context.customData as Data;
            d.lastUpdatedDict = new Dictionary<int, float>(context.segmentData.length);
            for (int i = 0; i < context.segmentData.length; i++)
            {
                d.lastUpdatedDict.Add(i, context.animatorContext.PassedTime);
            }
        }

        private void InitDelayDict(IAnimationContext context)
        {
            Data d = context.customData as Data;
            d.delayDict = new Dictionary<int, float>(context.segmentData.length);

            for (int i = 0; i < context.segmentData.length; i++)
            {
                d.delayDict.Add(i, 0);
            }
        }

        private void InitOffsetDict(IAnimationContext context)
        {
            Data d = context.customData as Data;
            d.offsetDict = new Dictionary<int, Vector2>(context.segmentData.length);

            for (int i = 0; i < context.segmentData.length; i++)
            {
                d.offsetDict.Add(i, Vector2.zero);
            }
        }

        private void InitAutoUpdateDict(IAnimationContext context)
        {
            Data d = context.customData as Data;
            d.autoUpdateDict = new Dictionary<int, bool>(context.segmentData.length);

            for (int i = 0; i < context.segmentData.length; i++)
            {
                d.autoUpdateDict.Add(i, false);
            }
        }

        public override void SetParameters(object customData, IDictionary<string, string> parameters)
        {
            if (parameters == null) return;

            Data d = customData as Data;
            if (TryGetFloatParameter(out float f, parameters, "maxXAmplitude", "maxXAmp", "maxXA", "maxX")) d.maxXAmplitude = f;
            if (TryGetFloatParameter(out f, parameters, "duration", "dur", "d")) d.duration = f;
            if (TryGetFloatParameter(out f, parameters, "maxYAmplitude", "maxYAmp", "maxYA", "maxY")) d.maxYAmplitude = f;
            if (TryGetFloatParameter(out f, parameters, "minXAmplitude", "minXAmp", "minXA", "minX")) d.minXAmplitude = f;
            if (TryGetFloatParameter(out f, parameters, "minYAmplitude", "minYAmp", "minYA", "minY")) d.minYAmplitude = f;
            if (TryGetBoolParameter(out bool b, parameters, "uniform", "uni", "u")) d.uniform = b;
            if (TryGetBoolParameter(out b, parameters, "uniformDelay", "uniDelay", "uniD", "uD")) d.uniformDelay = b;
            if (TryGetFloatParameter(out f, parameters, "minDelay", "minD")) d.minDelay = f;
            if (TryGetFloatParameter(out f, parameters, "maxDelay", "maxD")) d.maxDelay = f;
        }

        public override bool ValidateParameters(IDictionary<string, string> parameters)
        {
            if (parameters == null) return true;

            if (HasNonFloatParameter(parameters, "maxXAmplitude", "maxXAmp", "maxXA", "maxX")) return false;
            if (HasNonFloatParameter(parameters, "duration", "dur", "d")) return false;
            if (HasNonFloatParameter(parameters, "maxYAmplitude", "maxYAmp", "maxYA", "maxY")) return false;
            if (HasNonFloatParameter(parameters, "minXAmplitude", "minXAmp", "minXA", "minX")) return false;
            if (HasNonFloatParameter(parameters, "minYAmplitude", "minYAmp", "minYA", "minY")) return false;
            if (HasNonBoolParameter(parameters, "uniform", "uni", "u")) return false;
            if (HasNonBoolParameter(parameters, "uniformDelay", "uniDelay", "uniD", "uD")) return false;
            if (HasNonFloatParameter(parameters, "minDelay", "minD")) return false;
            if (HasNonFloatParameter(parameters, "maxDelay", "maxD")) return false;
            return true;
        }

        public override object GetNewCustomData()
        {
            return new Data()
            {
                uniform = this.uniform,
                duration = this.duration,
                maxXAmplitude = this.maxXAmplitude,
                minXAmplitude = this.minXAmplitude,
                minYAmplitude = this.minYAmplitude,
                maxYAmplitude = this.maxYAmplitude,
                uniformDelay = this.uniformDelay,
                minDelay = this.minDelay,
                maxDelay = this.maxDelay,
            };
        }

        private class Data
        {
            public float duration;
            public bool init = false;

            public bool uniform;
            public float maxXAmplitude;
            public float minXAmplitude;
            public float maxYAmplitude;
            public float minYAmplitude;
            public bool uniformDelay;
            public float minDelay;
            public float maxDelay;

            // uniform
            public System.Random rng = null;
            public float yOffset = 0;
            public float xOffset = 0;
            public float lastUpdated = 0;
            public float delay = 0;

            // uniform, non uniform delay
            public Dictionary<int, bool> autoUpdateDict = null;
            public int updatingIndex = -1;
            public float sharedDelay = 0;
            public float sharedLastUpdated = 0;

            // non uniform
            public Dictionary<int, Vector2> offsetDict = null;
            public Dictionary<int, float> lastUpdatedDict = null;
            public Dictionary<int, float> delayDict = null;
            public Dictionary<int, System.Random> rngDict = null;
        }
    }
}