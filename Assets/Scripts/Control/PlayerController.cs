#nullable enable
using System;
using UnityEngine;
using MinecraftClient.Event;
using MinecraftClient.Mapping;
using MinecraftClient.Rendering;

namespace MinecraftClient.Control
{
    [RequireComponent(typeof (Rigidbody), typeof (EntityRender), typeof (PlayerStatusUpdater))]
    public class PlayerController : MonoBehaviour, IPlayerController
    {
        [SerializeField] public PlayerAbility? playerAbility;
        [SerializeField] public Transform? cameraRef;
        [SerializeField] public Transform? visualTransform;
        [SerializeField] public PhysicMaterial? physicMaterial;

        private CornClient? game;
        private Rigidbody? playerRigidbody;
        private Collider? playerCollider;

        private readonly PlayerUserInputData inputData = new();
        private PlayerUserInput? userInput;
        private PlayerStatusUpdater? statusUpdater;
        public PlayerStatus? Status => statusUpdater?.Status;

        private PlayerInteractionUpdater? interactionUpdater;

        private IPlayerState CurrentState = PlayerStates.IDLE;
        private bool useRootMotion = false;
        public bool UseRootMotion
        {
            get {
                return useRootMotion;
            }

            set {
                useRootMotion = value;
            }
        }


        // Access for networking thread
        [HideInInspector] public Location ServerLocation;
        [HideInInspector] public float ServerYaw = 0F;
        [HideInInspector] public float ServerPitch = 0F;

        private CameraController? camControl;
        private IPlayerVisual? playerRender;
        private Entity fakeEntity = new(0, EntityType.Player, new());

        public void DisableEntity()
        {
            // Update components state...
            playerCollider!.enabled = false;
            playerRigidbody!.useGravity = false;

            // Reset player status
            playerRigidbody!.velocity = Vector3.zero;
            Status!.Grounded  = false;
            Status.InLiquid  = false;
            Status.OnWall    = false;
            Status.Sprinting = false;

            Status.Spectating = true;
            Status.GravityDisabled = true;
        }

        public void EnableEntity()
        {
            // Update components state...
            playerCollider!.enabled = true;
            playerRigidbody!.useGravity = true;

            Status!.Spectating = false;
            Status.GravityDisabled = false;
        }

        public void ToggleWalkMode()
        {
            Status!.WalkMode = !Status.WalkMode;
            CornClient.ShowNotification(Status.WalkMode ? "Switched to walk mode" : "Switched to rush mode");
        }

        public void CrossFadeState(string stateName, float time = 0.2F, int layer = 0, float timeOffset = 0F)
        {
            playerRender!.CrossFadeState(stateName, time, layer, timeOffset);
        }

        public void RandomizeMirroredFlag()
        {
            var mirrored = Time.frameCount % 2 == 0;
            playerRender!.SetMirroredFlag(mirrored);
            //Debug.Log($"Animation mirrored: {mirrored}");
        }

        public void SetEntityId(int entityId)
        {
            fakeEntity.ID = entityId;
            // Reassign this entity to refresh
            playerRender!.UpdateEntity(fakeEntity);
        }

        public void SetLocation(Location loc)
        {
            ServerLocation = loc;
            transform.position = CoordConvert.MC2Unity(loc);

            Debug.Log($"Position set to {transform.position}");
        }

        void Update() => ManagedUpdate(Time.deltaTime);

        public void ManagedUpdate(float interval)
        {
            if (!game!.IsMovementReady())
            {
                playerRigidbody!.useGravity = false;
                playerRigidbody!.velocity = Vector3.zero;

                Status!.GravityDisabled = true;

                transform.position = CoordConvert.MC2Unity(ServerLocation);
            }
            else
            {
                if (Status!.GravityDisabled != Status.Spectating)
                {
                    playerRigidbody!.useGravity = !Status.Spectating;
                    Status.GravityDisabled = !Status.Spectating;
                }
            }

            // Update user input
            userInput!.UpdateInputs(inputData, game!.PlayerData.Perspective);

            // Update player status (in water, grounded, etc)
            if (!Status.Spectating)
                statusUpdater!.UpdatePlayerStatus(game!.GetWorld(), visualTransform!.forward);

            // Update target block selection
            interactionUpdater!.UpdateBlockSelection(camControl!.GetViewportCenterRay());

            // Update player interactions
            interactionUpdater.UpdateInteractions(game!.GetWorld());

            var status = statusUpdater!.Status;

            // Update current player state
            if (CurrentState.ShouldExit(status))
            {
                // Try to exit current state and enter another one
                foreach (var state in PlayerStates.STATES)
                {
                    if (state != CurrentState && state.ShouldEnter(status))
                    {
                        CurrentState.OnExit(status, playerAbility!, playerRigidbody!, this);

                        // Exit previous state and enter this state
                        CurrentState = state;
                        
                        CurrentState.OnEnter(status, playerAbility!, playerRigidbody!, this);
                        break;
                    }
                }
            }

            Status.CurrentVisualYaw = visualTransform!.eulerAngles.y;

            float prevStamina = status.StaminaLeft;

            // Prepare current and target player visual yaw before updating it
            if (inputData.horInputNormalized != Vector2.zero)
            {
                Status.UserInputYaw = AngleConvert.GetYawFromVector2(inputData.horInputNormalized);
                Status.TargetVisualYaw = camControl!.GetYaw() + Status.UserInputYaw;
            }
            
            // Update player physics and transform using updated current state
            CurrentState.UpdatePlayer(interval, inputData, status, playerAbility!, playerRigidbody!, this);

            // Broadcast current stamina if changed
            if (prevStamina != status.StaminaLeft)
                EventManager.Instance.Broadcast<StaminaUpdateEvent>(new(status.StaminaLeft, status.StaminaLeft >= playerAbility!.MaxStamina));

            // Apply updated visual yaw to visual transform
            visualTransform!.eulerAngles = new(0F, Status.CurrentVisualYaw, 0F);
            
            // Update player render state machine
            playerRender!.UpdateStateMachine(status);
            // Update player render velocity
            playerRender.UpdateVelocity(playerRigidbody!.velocity);

            // Update render
            playerRender!.UpdateVisual(game!.GetTickMilSec());

            // Tell server our current position
            Location rawLocation;

            if (CurrentState is ForceMoveState)
            // Use move origin as the player location to tell to server, to
            // prevent sending invalid positions during a force move operation
                rawLocation = CoordConvert.Unity2MC(((ForceMoveState) CurrentState).GetFakePlayerOffset());
            else
                rawLocation = CoordConvert.Unity2MC(transform.position);

            // Preprocess the location before sending it (to avoid position correction from server)
            if ((status.Grounded || status.CenterDownDist < 0.5F) && rawLocation.Y - (int)rawLocation.Y > 0.9D)
                rawLocation.Y = (int)rawLocation.Y + 1;

            // Get server yaw for networking thread
            ServerLocation = rawLocation;
            ServerYaw = visualTransform!.eulerAngles.y - 90F;
        }

        public void StartForceMoveOperation(string name, ForceMoveOperation[] ops)
        {
            CurrentState.OnExit(statusUpdater!.Status, playerAbility!, playerRigidbody!, this);

            // Enter a new force move state
            CurrentState = new ForceMoveState(name, ops);
            CurrentState.OnEnter(statusUpdater!.Status, playerAbility!, playerRigidbody!, this);
        }
 
        private Action<PerspectiveUpdateEvent>? perspectiveCallback;
        private Action<GameModeUpdateEvent>? gameModeCallback;

        void Start()
        {
            camControl = GameObject.FindObjectOfType<CameraController>();
            game = CornClient.Instance;
            
            // Initialize player visuals
            playerRender = GetComponent<IPlayerVisual>();

            fakeEntity.Name = game!.GetUsername();
            fakeEntity.ID   = 0;
            playerRender.UpdateEntity(fakeEntity);

            playerRigidbody = GetComponent<Rigidbody>();
            playerRigidbody.useGravity = true;

            statusUpdater = GetComponent<PlayerStatusUpdater>();
            userInput = GetComponent<PlayerUserInput>();
            interactionUpdater = GetComponent<PlayerInteractionUpdater>();

            perspectiveCallback = (e) => { };

            gameModeCallback = (e) => {
                if (e.GameMode != GameMode.Spectator && game!.LocationReceived)
                    EnableEntity();
                else
                    DisableEntity();
            };

            EventManager.Instance.Register(perspectiveCallback);
            EventManager.Instance.Register(gameModeCallback);

            if (playerAbility is null)
                Debug.LogError("Player ability not assigned!");
            else
            {
                var boxcast = playerAbility.ColliderType == PlayerAbility.PlayerColliderType.Box;

                if (boxcast)
                {
                    // Attach box collider
                    var box = gameObject.AddComponent<BoxCollider>();
                    var sideLength = playerAbility.ColliderRadius * 2F;
                    box.size = new(sideLength, playerAbility.ColliderHeight, sideLength);
                    box.center = new(0F, playerAbility.ColliderHeight / 2F, 0F);

                    statusUpdater.GroundBoxcastHalfSize = new(playerAbility.ColliderRadius, 0.01F, playerAbility.ColliderRadius);

                    playerCollider = box;
                }
                else
                {
                    // Attach capsule collider
                    var capsule = gameObject.AddComponent<CapsuleCollider>();
                    capsule.height = playerAbility.ColliderHeight;
                    capsule.radius = playerAbility.ColliderRadius;
                    capsule.center = new(0F, playerAbility.ColliderHeight / 2F, 0F);

                    statusUpdater.GroundSpherecastRadius = playerAbility.ColliderRadius;
                    statusUpdater.GroundSpherecastCenter = new(0F, playerAbility.ColliderRadius + 0.05F, 0F);

                    playerCollider = capsule;
                }

                playerCollider.material = physicMaterial;

                statusUpdater.UseBoxCastForGroundedCheck = boxcast;

                // Set stamina to max value
                Status!.StaminaLeft = playerAbility!.MaxStamina;
                // And broadcast current stamina
                EventManager.Instance.Broadcast<StaminaUpdateEvent>(new(Status.StaminaLeft, true));
                // Initialize health value
                EventManager.Instance.Broadcast<HealthUpdateEvent>(new(20F, true));
                game.PlayerData.MaxHealth = 20F;
            }

        }

        void OnDestroy()
        {
            if (perspectiveCallback is not null)
                EventManager.Instance.Unregister(perspectiveCallback);

            if (gameModeCallback is not null)
                EventManager.Instance.Unregister(gameModeCallback);
        }

        public string GetDebugInfo()
        {
            string targetBlockInfo = string.Empty;
            var loc = ServerLocation;
            var loc2 = game!.GetCurrentLocation();
            var world = game!.GetWorld();

            var target = interactionUpdater!.TargetLocation;

            if (target is not null)
            {
                var targetBlock = world?.GetBlock(target.Value);
                if (targetBlock is not null)
                    targetBlockInfo = targetBlock.ToString();
            }

            var lightInfo = $"sky {world?.GetSkyLight(loc)} block {world?.GetBlockLight(loc)}";

            var velocity = playerRigidbody!.velocity;
            // Visually swap xz velocity to fit vanilla
            var veloInfo = $"Vel:\t{velocity.z:0.00}\t{velocity.y:0.00}\t{velocity.x:0.00}\n({velocity.magnitude:0.000})";

            if (statusUpdater!.Status.Spectating)
                return $"Lcl Pos:\t{loc}\nSvr Pos:\t{loc2}\nState:\t{CurrentState}\n{veloInfo}\nLighting:\t{lightInfo}\nTarget Block:\t{target}\n{targetBlockInfo}\nBiome:\n[{world?.GetBiomeId(loc)}] {world?.GetBiome(loc).GetDescription()}";
            else
                return $"Lcl Pos:\t{loc}\nSvr Pos:\t{loc2}\nState:\t{CurrentState}\n{veloInfo}\nLighting:\t{lightInfo}\n{statusUpdater!.Status}\nTarget Block:\t{target}\n{targetBlockInfo}\nBiome:\n[{world?.GetBiomeId(loc)}] {world?.GetBiome(loc).GetDescription()}";

        }

    }
}
