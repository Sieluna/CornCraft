#nullable enable
using UnityEngine;
using MinecraftClient.Control;
using MinecraftClient.Mapping;

namespace MinecraftClient.Rendering
{
    public class PlayerEntityAnimeRender : EntityRender, IPlayerVisual
    {
        private static readonly int GROUNDED = Animator.StringToHash("Grounded");
        private static readonly int IN_LIQUID = Animator.StringToHash("InLiquid");
        private static readonly int ON_WALL = Animator.StringToHash("OnWall");
        private static readonly int MOVING = Animator.StringToHash("Moving");
        private static readonly int SPRINTING = Animator.StringToHash("Sprinting");
        private static readonly int WALK_MODE = Animator.StringToHash("WalkMode");
        private static readonly int CENTER_DOWN_DIST = Animator.StringToHash("CenterDownDist");

        private static readonly int FORCED_ANIMATION = Animator.StringToHash("ForcedAnimation");

        private static readonly int VERTICAL_SPEED = Animator.StringToHash("VerticalSpeed");
        private static readonly int HORIZONTAL_SPEED = Animator.StringToHash("HorizontalSpeed");

        private Animator? playerAnimator;

        private bool animatorPresent = false;

        protected override void Initialize()
        {
            base.Initialize();

            // Get animator component
            playerAnimator = GetComponentInChildren<Animator>();

            if (playerAnimator is not null)
                animatorPresent = true;
            else
                Debug.LogWarning("Player animator not found!");

        }

        public void UpdateEntity(Entity entity) => base.Entity = entity;

        public override void SetCurrentVelocity(Vector3 velocity)
        {
            base.SetCurrentVelocity(velocity);

            if (animatorPresent)
            {
                playerAnimator!.SetFloat(VERTICAL_SPEED, velocity.y);
                playerAnimator.SetFloat(HORIZONTAL_SPEED, Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z));
            }
        }

        public void UpdateVelocity(Vector3 velocity) => SetCurrentVelocity(velocity);

        public void UpdateStateMachine(PlayerStatus info)
        {
            if (animatorPresent)
            {
                // Update animator parameters
                playerAnimator!.SetBool(GROUNDED, info.Grounded);
                playerAnimator.SetBool(IN_LIQUID, info.InLiquid);
                playerAnimator.SetBool(ON_WALL, info.OnWall);
                playerAnimator.SetBool(MOVING, info.Moving);
                playerAnimator.SetBool(SPRINTING, info.Sprinting);
                playerAnimator.SetBool(WALK_MODE, info.WalkMode);
                playerAnimator.SetFloat(CENTER_DOWN_DIST, info.CenterDownDist);

                playerAnimator.SetBool(FORCED_ANIMATION, info.PlayingForcedAnimation);

            }
        }

        public void CrossFadeState(string stateName, float time, int layer, float timeOffset)
        {
            if (animatorPresent)
                playerAnimator!.CrossFade(stateName, time, layer, timeOffset);
        }

    }
}