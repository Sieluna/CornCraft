#nullable enable
using UnityEngine;

namespace MinecraftClient.Control
{
    public class SwimState : IPlayerState
    {
        public const float THRESHOLD_LAND_PLATFORM  = -1.4F;
        public const float SURFING_LIQUID_DIST_THERSHOLD = -0.2F;

        public void UpdatePlayer(float interval, PlayerUserInputData inputData, PlayerStatus info, Rigidbody rigidbody, PlayerController player)
        {
            var ability = player.Ability;
            
            info.Sprinting = false;
            info.Moving = true;

            var moveSpeed = (info.WalkMode ? ability.WalkSpeed : ability.RunSpeed) * ability.WaterMoveMultiplier;

            var distAboveLiquidSurface = info.LiquidDist - SURFING_LIQUID_DIST_THERSHOLD;

            Vector3 moveVelocity;

            bool costStamina = true;

            if (inputData.horInputNormalized != Vector2.zero) // Move horizontally
            {
                // Smooth rotation for player model
                info.CurrentVisualYaw = Mathf.LerpAngle(info.CurrentVisualYaw, info.TargetVisualYaw, ability.SteerSpeed * interval);
                // Use the target visual yaw as actual movement direction
                moveVelocity = Quaternion.AngleAxis(info.TargetVisualYaw, Vector3.up) * Vector3.forward * moveSpeed;

                if (info.FrontDownDist < -0.05F) // A platform ahead of the player
                {
                    if (info.LiquidDist > info.FrontDownDist) // On water suface now, and the platform is above water
                    {
                        if (info.LiquidDist > THRESHOLD_LAND_PLATFORM)
                        {
                            var moveHorDir = Quaternion.AngleAxis(info.TargetVisualYaw, Vector3.up) * Vector3.forward;
                            var horOffset = info.BarrierDist - 1.0F;

                            // Start force move operation
                            var org  = rigidbody.transform.position;
                            var dest = org + (-info.FrontDownDist - 0.95F) * Vector3.up + moveHorDir * horOffset;

                            player.StartForceMoveOperation("Climb to land",
                                    new ForceMoveOperation[] {
                                            new(org,  dest, 0.01F + Mathf.Max(info.LiquidDist - info.FrontDownDist - 0.2F, 0F) * 0.5F),
                                            new(dest, ability.Climb1mCurves, player.visualTransform!.rotation, 0F, 1.1F,
                                                init: (info, rigidbody, player) => {
                                                    player.RandomizeMirroredFlag();
                                                    player.CrossFadeState(PlayerAbility.CLIMB_1M);
                                                    player.UseRootMotion = true;
                                                },
                                                update: (interval, inputData, info, rigidbody, player) =>
                                                    info.Moving = inputData.horInputNormalized != Vector2.zero,
                                                exit: (info, rigidbody, player) => {
                                                    player.UseRootMotion = false;
                                                }
                                            )
                                    } );
                        }
                        // Otherwise the platform/land is too high to reach
                    }
                    else if (info.LiquidDist > THRESHOLD_LAND_PLATFORM) // Approaching Water suface, and the platform is under water 
                    {
                        var moveHorDir = Quaternion.AngleAxis(info.TargetVisualYaw, Vector3.up) * Vector3.forward;

                        // Start force move operation
                        var org  = rigidbody.transform.position;
                        var dest = org + (-info.FrontDownDist) * Vector3.up + moveHorDir * 0.55F;

                        player.StartForceMoveOperation("Swim over barrier underwater",
                                new ForceMoveOperation[] {
                                        new(org,  dest, 0.8F,
                                            update: (interval, inputData, info, rigidbody, player) =>
                                                info.Moving = true
                                        )
                                } );
                    }
                    else // Below water surface now, swim up a bit
                    {
                        var moveHorDir = Quaternion.AngleAxis(info.TargetVisualYaw, Vector3.up) * Vector3.forward;

                        // Start force move operation
                        var org  = rigidbody.transform.position;
                        var dest = org + (-info.FrontDownDist + 0.2F) * Vector3.up + moveHorDir * 0.35F;

                        player.StartForceMoveOperation("Swim over barrier underwater",
                                new ForceMoveOperation[] {
                                        new(org,  dest, 0.5F,
                                            update: (interval, inputData, info, rigidbody, player) =>
                                                info.Moving = true
                                        )
                                } );
                    }
                }
            }
            else
                moveVelocity = Vector3.zero;
            
            // Whether movement should be slowed down by liquid, and whether the player can move around by swimming
            bool movementAffected = info.LiquidDist <= -0.4F;

            if (movementAffected) // In liquid
                moveVelocity.y = Mathf.Max(rigidbody.velocity.y, ability.MaxInLiquidFallSpeed);
            else // Still in air, free fall
                moveVelocity.y = rigidbody.velocity.y;

            // Check vertical movement...
            if (inputData.ascend)
            {
                if (distAboveLiquidSurface > 0F) // Free fall in air
                {
                    costStamina = false;
                }
                else if (distAboveLiquidSurface > -0.5F) // Cancel gravity, but don't move up further
                {
                    moveVelocity.y = -distAboveLiquidSurface;
                    costStamina = false;
                }
                else
                    moveVelocity.y =  moveSpeed; // Move up
            }
            else if (inputData.descend)
            {
                if (!info.Grounded)
                    moveVelocity.y = -moveSpeed;
            }
            else // Not moving horizontally
            {
                if (inputData.horInputNormalized != Vector2.zero) // Moving, cancel gravity
                {
                    if (movementAffected)
                    {
                        if (distAboveLiquidSurface > -0.5F)
                            moveVelocity.y = -distAboveLiquidSurface;
                        else
                            moveVelocity.y =  0.5F;
                    }
                    // Otherwise free fall
                }
                
            }

            // Apply new velocity to rigidbody
            rigidbody.AddForce((moveVelocity - rigidbody.velocity) * 0.2F, ForceMode.VelocityChange);
            
            if (info.Grounded) // Restore stamina
                info.StaminaLeft = Mathf.MoveTowards(info.StaminaLeft, ability.MaxStamina, interval * ability.StaminaRestore);
            else if (costStamina) // Update stamina
                info.StaminaLeft = Mathf.MoveTowards(info.StaminaLeft, 0F, interval * ability.SwimStaminaCost);

        }

        public bool ShouldEnter(PlayerUserInputData inputData, PlayerStatus info)
        {
            if (inputData.horInputNormalized == Vector2.zero && !inputData.ascend && !inputData.descend)
                return false;

            if (!info.Spectating && info.InLiquid)
                return true;
            
            return false;
        }

        public bool ShouldExit(PlayerUserInputData inputData, PlayerStatus info)
        {
            if (inputData.horInputNormalized == Vector2.zero && !inputData.ascend && !inputData.descend)
                return true;
            
            if (info.Spectating || !info.InLiquid)
                return true;
            
            return false;
        }

        public override string ToString() => "Swim";

    }
}