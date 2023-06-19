#nullable enable
using UnityEngine;

using MinecraftClient.Mapping;
using MinecraftClient.Rendering;

namespace MinecraftClient.Control
{
    [RequireComponent(typeof (Rigidbody), typeof (EntityRender), typeof (PlayerStatusUpdater))]
    public class VanillaPlayerController : PlayerController
    {
        private PlayerEntityVanillaRender? playerRender;

        public override void InitializePlayer(Entity clientEntity, GameMode initGameMode)
        {
            base.InitializePlayer(clientEntity, initGameMode);

            // Initialize player render
            playerRender = GetComponent<PlayerEntityVanillaRender>();
            playerRender.Initialize(clientEntity.Type, clientEntity);
        }

        void Update() => LogicalUpdate(Time.deltaTime);

        void FixedUpdate() => PhysicalUpdate(Time.fixedDeltaTime);

        protected override void LogicalUpdate(float interval)
        {
            PreLogicalUpdate(interval);
            
            if (playerRender != null)
            {
                // Update player render velocity
                playerRender.SetVisualMovementVelocity(playerRigidbody!.velocity);
                // Update render
                playerRender.UpdateAnimation(client!.GetTickMilSec());
            }

            // Disable attack
            if (Status!.AttackStatus.AttackCooldown > 1F)
            {
                Status!.AttackStatus.AttackCooldown = 1F;
            }

            PostLogicalUpdate();
        }
    }
}
