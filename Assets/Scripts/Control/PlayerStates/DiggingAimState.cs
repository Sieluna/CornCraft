﻿#nullable enable
using KinematicCharacterController;
using UnityEngine;

namespace CraftSharp.Control
{
    public class DiggingAimState : IPlayerState
    {
        private const float DIGGING_SETUP_TIME = 0.5F;
        private const float DIGGING_IDLE_TIMEEOUT = -0.1F;

        public void UpdateMain(ref Vector3 currentVelocity, float interval, PlayerActions inputData, PlayerStatus info, KinematicCharacterMotor motor, PlayerController player)
        {
            var ability = player.AbilityConfig;

            info.Sprinting = false;
            info.Gliding = false;

            var attackStatus = info.AttackStatus;

            // Stay in this state if attack button is still pressed or the initiation phase is not yet complete
            if (attackStatus.StageTime < DIGGING_SETUP_TIME || inputData.Attack.ChargedAttack.IsPressed())
            {
                // Update moving status
                bool prevMoving = info.Moving;
                info.Moving = inputData.Gameplay.Movement.IsPressed();

                // Animation mirror randomation
                if (info.Moving != prevMoving)
                {
                    player.RandomizeMirroredFlag();
                }

                // Player can only move slowly in this state
                var moveSpeed = ability.WalkSpeed;
                Vector3 moveVelocity;

                // Use target orientation to calculate actual movement direction, taking ground shape into consideration
                if (info.Moving)
                {
                    moveVelocity = motor.GetDirectionTangentToSurface(player.GetTargetOrientation() * Vector3.forward, motor.GroundingStatus.GroundNormal) * moveSpeed;
                }
                else // Idle
                {
                    moveVelocity = Vector3.zero;
                }

                // Apply updated velocity
                currentVelocity = moveVelocity;

                attackStatus.StageTime += interval;
                // Reset cooldown
                attackStatus.AttackCooldown = 0F;
            }
            else // Digging state ends
            {
                // Idle timeout
                attackStatus.AttackCooldown -= interval;

                if (attackStatus.AttackCooldown < DIGGING_IDLE_TIMEEOUT) // Timed out, exit state
                {
                    // Attack timed out, exit
                    info.Attacking = false;
                    attackStatus.AttackStage = -1;
                }
            }

            // Restore stamina
            info.StaminaLeft = Mathf.MoveTowards(info.StaminaLeft, ability.MaxStamina, interval * ability.StaminaRestore);
        }

        public bool ShouldEnter(PlayerActions inputData, PlayerStatus info)
        {
            // State only available via direct transition
            return false;
        }

        public bool ShouldExit(PlayerActions inputData, PlayerStatus info)
        {
            if (!info.Attacking)
                return true;

            if (info.Spectating)
                return true;

            return false;
        }

        public void OnEnter(IPlayerState prevState, PlayerStatus info, KinematicCharacterMotor motor, PlayerController player)
        {
            // Digging also use this attacking flag
            info.Attacking = true;

            var attackStatus = info.AttackStatus;

            attackStatus.AttackStage = 0;
            attackStatus.StageTime = 0F;

            player.ChangeItemState(PlayerController.CurrentItemState.HoldInOffhand);

            player.StartAiming();
        }

        public void OnExit(IPlayerState nextState, PlayerStatus info, KinematicCharacterMotor motor, PlayerController player)
        {
            // Digging also use this attacking flag
            info.Attacking = false;

            var attackStatus = info.AttackStatus;
            attackStatus.AttackCooldown = 0F;

            player.ChangeItemState(PlayerController.CurrentItemState.Mount);

            player.StopAiming();
        }

        public override string ToString() => "DiggingAim";
    }
}