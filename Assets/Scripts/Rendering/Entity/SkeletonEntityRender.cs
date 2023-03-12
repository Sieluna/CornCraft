#nullable enable
using UnityEngine;
using MinecraftClient.Mapping;

namespace MinecraftClient.Rendering
{
    public class SkeletonEntityRender : BipedEntityRender
    {
        public Transform? leftArm, rightArm;

        protected bool armsPresent = false;

        public override void Initialize(EntityType entityType, Entity entity)
        {
            base.Initialize(entityType, entity);

            if (leftArm is null || rightArm is null)
                Debug.LogWarning("Arms of skeleton entity not properly assigned!");
            else
                armsPresent = true;
        }

        public override void UpdateAnimation(float tickMilSec)
        {
            base.UpdateAnimation(tickMilSec);

            if (armsPresent)
            {
                leftArm!.localEulerAngles  = new(-90F, 30F, 0F);
                rightArm!.localEulerAngles = new(-90F,  0F, 0F);
            }

        }

    }
}
