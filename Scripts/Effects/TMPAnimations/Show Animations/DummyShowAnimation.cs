using System.Collections.Generic;
using TMPEffects.Components.CharacterData;

namespace TMPEffects.TMPAnimations.ShowAnimations
{
    public class DummyShowAnimation : TMPShowAnimation
    {
        public override void Animate(ref CharData cData, IAnimationContext context)
        {
            for (int i = 0; i < 4; i++)
            {
                cData.SetVertex(i, cData.mesh.initial.GetPosition(i));
            }

            cData.SetVisibilityState(VisibilityState.Shown, context.animatorContext.PassedTime);
        }

        public override void ResetParameters()
        {
        }

        public override void SetParameters(IDictionary<string, string> parameters)
        {
        }

        public override bool ValidateParameters(IDictionary<string, string> parameters)
        {
            return true;
        }
    }
}