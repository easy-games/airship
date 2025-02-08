using System;
using Code.Player.Character.Net;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement.BasicTest
{
    public class BasicTestMovement : NetworkedMovement<BasicMovementState, BasicMovementInput>
    {
        private Rigidbody rb;
        private Vector3 moveVector;
        private MovementMode mode;

        private void Awake()
        {
            rb = this.GetComponent<Rigidbody>();
        }

        public override void OnSetMode(MovementMode mode)
        {
            this.mode = mode;
            
            if (mode == MovementMode.Observer)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }

            if (mode == MovementMode.Authority || mode == MovementMode.Input)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        public override void OnSetPaused(bool paused)
        {
            if (mode == MovementMode.Input || mode == MovementMode.Authority)
            {
                if (paused)
                {
                    this.rb.isKinematic = true;
                }
                else
                {
                    this.rb.isKinematic = false;
                }
            }
        }

        public override void SetCurrentState(BasicMovementState state)
        {
            rb.position = state.position;
            rb.rotation = state.rotation;
            if (!rb.isKinematic)
            {
                rb.velocity = state.velocity;
                rb.angularVelocity = state.angularVelocity;
            }
        }

        public override BasicMovementState GetCurrentState(int commandNumber, double time)
        {
            return new BasicMovementState()
            {
                position = rb.position, rotation = rb.rotation, velocity = rb.velocity,
                angularVelocity = rb.angularVelocity, lastProcessedCommand = commandNumber, time = time
            };
        }

        public override void Tick(BasicMovementInput command, bool replay)
        {
            if (command == null) return;
            if (replay) Debug.Log("Replaying" + command.commandNumber);
            rb.MovePosition(rb.position + command.moveDirection * Time.fixedDeltaTime * 10f);
            //rb.position = rb.position + command.moveDirection * Time.fixedDeltaTime * 10f;
            
            //rb.velocity = command.moveDirection * 5f;
        }

        public override void Interpolate(float delta, BasicMovementState stateOld, BasicMovementState stateNew)
        {
            this.rb.position = Vector3.Lerp(stateOld.position, stateNew.position, delta);
            this.rb.rotation = Quaternion.Lerp(stateOld.rotation, stateNew.rotation, delta);
        }

        public override BasicMovementInput GetCommand(int commandNumber, double time)
        {
            return new BasicMovementInput() { moveDirection = moveVector, commandNumber = commandNumber, time = time};
        }

        private void Update()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");
            this.moveVector = new Vector3(moveX, 0f, moveZ).normalized;
        }
    }
}