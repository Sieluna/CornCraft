#nullable enable
using UnityEngine;

namespace CraftSharp.Control
{
    public class ForceMoveState : IPlayerState
    {
        public readonly string Name;
        public readonly ForceMoveOperation[] Operations;

        private int currentOperationIndex = 0;

        private ForceMoveOperation? currentOperation;

        private float currentTime = 0F;

        public ForceMoveState(string name, ForceMoveOperation[] op)
        {
            Name = name;
            Operations = op;
        }

        public void UpdatePlayer(float interval, PlayerActions inputData, PlayerStatus info, Rigidbody rigidbody, PlayerController player)
        {
            if (currentOperation is null)
                return;

            currentTime = Mathf.Max(currentTime - interval, 0F);

            if (currentTime <= 0F)
            {
                // Finish current operation
                FinishOperation(info, rigidbody, player);

                currentOperationIndex++;

                if (currentOperationIndex < Operations.Length)
                {
                    // Start next operation in sequence
                    StartOperation(info, rigidbody, player);
                }
            }
            else
            {
                switch (currentOperation.DisplacementType)
                {
                    case ForceMoveDisplacementType.FixedDisplacement:
                        var moveProgress = currentTime / currentOperation.TimeTotal;
                        var curPosition1 = Vector3.Lerp(currentOperation.Destination!.Value, currentOperation.Origin, moveProgress);

                        //rigidbody.position = curPosition1;
                        //player.transform.position = curPosition1;
                        rigidbody.MovePosition(curPosition1);
                        break;
                    case ForceMoveDisplacementType.CurvesDisplacement:
                        // Sample animation 
                        var curPosition2 = currentOperation.SampleTargetAt(currentOperation.TimeTotal - currentTime);
                        //rigidbody.position = curPosition2;
                        player.transform.position = curPosition2;
                        //rigidbody.MovePosition(curPosition2);
                        break;
                    case ForceMoveDisplacementType.RootMotionDisplacement:
                        // Do nothing
                        
                        break;
                }

                // Call operation update
                currentOperation.OperationUpdate?.Invoke(interval, inputData, info, rigidbody, player);
            }
        }

        private void StartOperation(PlayerStatus info, Rigidbody rigidbody, PlayerController player)
        {
            // Update current operation
            currentOperation = Operations[currentOperationIndex];

            if (currentOperation is not null)
            {
                // Invoke operation init if present
                currentOperation.OperationInit?.Invoke(info, rigidbody, player);
                
                currentTime = currentOperation.TimeTotal;

                switch (currentOperation.DisplacementType)
                {
                    case ForceMoveDisplacementType.FixedDisplacement:
                        //rigidbody.isKinematic = true;
                        break;
                    case ForceMoveDisplacementType.CurvesDisplacement:
                        info.PlayingRootMotion = true;
                        rigidbody.isKinematic = true;
                        break;
                    case ForceMoveDisplacementType.RootMotionDisplacement:
                        info.PlayingRootMotion = true;
                        break;
                }
            }
        }

        private void FinishOperation(PlayerStatus info, Rigidbody rigidbody, PlayerController player)
        {
            if (currentOperation is not null)
            {
                currentOperation.OperationExit?.Invoke(info, rigidbody, player);

                switch (currentOperation.DisplacementType)
                {
                    case ForceMoveDisplacementType.FixedDisplacement:
                        // Perform last move with rigidbody.MovePosition()
                        rigidbody!.MovePosition(currentOperation.Destination!.Value);
                        //rigidbody.isKinematic = false;
                        break;
                    case ForceMoveDisplacementType.CurvesDisplacement:
                        // Update ground dist
                        player.UpdatePlayerStatus();
                        //Debug.Log($"Ground dist after movement: {info.CenterDownDist}");
                        if (info.CenterDownDist < 0F) // Move upward a bit to avoid rigidbody from bouncing off the ground
                        {
                            player.transform.position += new Vector3(0F, -info.CenterDownDist, 0F);
                        }
                        info.PlayingRootMotion = false;
                        rigidbody.MovePosition(player.transform.position);
                        rigidbody.isKinematic = false;
                        rigidbody.velocity = Vector3.zero;

                        info.Grounded = true;
                        break;
                    case ForceMoveDisplacementType.RootMotionDisplacement:
                        info.PlayingRootMotion = false;
                        break;
                }
            }
        }

        public Vector3 GetFakePlayerOffset()
        {
            if (currentOperation is not null)
            {
                return currentOperation.Origin;
            }

            return Vector3.zero;
        }

        // This is not used, use PlayerController.StartForceMoveOperation() to enter this state
        public bool ShouldEnter(PlayerActions inputData, PlayerStatus info) => false;

        public bool ShouldExit(PlayerActions inputData, PlayerStatus info) =>
                currentOperationIndex >= Operations.Length && currentTime <= 0F;

        public void OnEnter(PlayerStatus info, Rigidbody rigidbody, PlayerController player)
        {
            if (Operations.Length > currentOperationIndex)
                StartOperation(info, rigidbody, player);
            
            //info.PlayingForcedAnimation = true;
        }

        public void OnExit(PlayerStatus info, Rigidbody rigidbody, PlayerController player)
        {
            //info.PlayingForcedAnimation = false;
        }

        public override string ToString() => $"ForceMove [{Name}] {currentOperationIndex + 1}/{Operations.Length} ({currentTime:0.00}/{currentOperation?.TimeTotal:0.00})";
    }
}