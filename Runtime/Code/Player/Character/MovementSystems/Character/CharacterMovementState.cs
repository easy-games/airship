// using System;
// using Assets.Luau;
// using Code.Player.Character.Net;
// using UnityEngine;
//
// namespace Code.Player.Character.NetworkedMovement
// {
//     public enum CharacterState
//     {
//         Idle = 0,
//         Running = 1,
//         Airborne = 2,
//         Sprinting = 3,
//         Crouching = 4,
//     }
//
//     public class CharacterMovementState : StateSnapshot, IEquatable<CharacterMovementState>
//     {
//         public int lastCommandProcessed { get; }
//         public Vector3 position;
//         public Vector3 velocity;
//         public bool inputDisabled;
//         public bool isFlying;
//         public int jumpCount;
//         public bool airborneFromImpulse;
//         public bool alreadyJumped; //Only lets the character jump once until the jump key is released
//         public Vector3 prevMoveDir; //Only used for firing an event
//
//         public Vector3
//             lastGroundedMoveDir; //What direction were we moving on the ground, so we can float that dir in the air
//
//         public bool prevCrouch;
//         public bool prevStepUp;
//         public bool prevGrounded;
//         public CharacterState state;
//         public CharacterState prevState;
//         public float timeSinceBecameGrounded;
//         public float timeSinceWasGrounded;
//         public float timeSinceJump;
//         public BinaryBlob customData;
//         
//         public CharacterMovementState(int commandNumber, Vector3 pos, Vector3 vel, bool inputDisabled, bool isFlying,
//             int jumpCount, bool airborneFromImpulse, bool alreadyJumped, Vector3 prevMoveDir,
//             Vector3 lastGroundedMoveDir, bool prevCrouch, bool prevStepUp, bool prevGrounded, CharacterState state, CharacterState prevState,
//             float timeSinceBecameGrounded, float timeSinceWasGrounded, float timeSinceJump, BinaryBlob customData)
//         {
//             this.position = pos;
//             this.velocity = vel;
//             this.lastCommandProcessed = commandNumber;
//             this.inputDisabled = inputDisabled;
//             this.isFlying = isFlying;
//             this.jumpCount = jumpCount;
//             this.airborneFromImpulse = airborneFromImpulse;
//             this.alreadyJumped = alreadyJumped;
//             this.prevMoveDir = prevMoveDir;
//             this.lastGroundedMoveDir = lastGroundedMoveDir;
//             this.prevCrouch = prevGrounded;
//             this.prevStepUp = prevStepUp;
//             this.prevGrounded = prevGrounded;
//             this.state = state;
//             this.prevState = prevState;
//             this.timeSinceBecameGrounded = timeSinceBecameGrounded;
//             this.timeSinceWasGrounded = timeSinceWasGrounded;
//             this.timeSinceJump = timeSinceJump;
//             this.customData = customData;
//         }
//
//         public bool Equals(CharacterMovementState other)
//         {
//             return
//                 velocity == other.velocity &&
//                 position == other.position &&
//                 state == other.state &&
//                 inputDisabled == other.inputDisabled &&
//                 isFlying == other.isFlying &&
//                 jumpCount == other.jumpCount &&
//                 airborneFromImpulse == other.airborneFromImpulse &&
//                 alreadyJumped == other.alreadyJumped &&
//                 prevMoveDir == other.prevMoveDir;
//         }
//     }
// }