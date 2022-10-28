#nullable enable
using UnityEngine;
using MinecraftClient.Mapping;

namespace MinecraftClient.Control
{
    public class PlayerStatus
    {
        // Player status
        public bool InWater  = false;
        public bool OnWaterSurface = false;

        public bool Grounded = false;
        public bool OnWall   = false;
        public bool Moving   = false;

        public bool Attacking  = false;
        public bool Spectating = false;
        public bool WalkMode   = false;

        // Reference values
        public float CenterDownDist = 0F;
        public float FrontDownDist  = 0F;

        public float UserInputYaw     = 0F;
        public float TargetVisualYaw  = 0F;
        public float CurrentVisualYaw = 0F;

        public float YawOffset => Mathf.Abs(Mathf.DeltaAngle(TargetVisualYaw, CurrentVisualYaw));

        // Block selection data
        public Location? TargetBlockPos = null;

        public override string ToString() => 
            $"InWater:\t{InWater}\n* Surfing:\t{OnWaterSurface}\nGrounded:\t{Grounded}\nClimbing:\t{OnWall}\nMoving:\t{Moving}\t{CenterDownDist:0.00}\t{FrontDownDist:0.00}\nYaw delta:\t{YawOffset:0.00}";

    }
}