
namespace TMPEffects.Components
{
    public interface ITMPAnimator
    {
        public bool IsAnimating { get; }


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
    }
}
