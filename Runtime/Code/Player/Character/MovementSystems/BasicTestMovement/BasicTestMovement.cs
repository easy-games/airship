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

        private void Awake()
        {
            rb = this.GetComponent<Rigidbody>();
            rb.isKinematic = true;
        }

        public override void SetCurrentState(BasicMovementState state)
        {
            rb.position = state.position;
            rb.rotation = state.rotation;
            rb.velocity = state.velocity;
            rb.angularVelocity = state.angularVelocity;
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
            if (replay) Debug.Log("Replaying" + command.commandNumber);
            //rb.MovePosition(rb.position + command.moveDirection * Time.fixedDeltaTime * 10f);
            rb.position = rb.position + command.moveDirection * Time.fixedDeltaTime * 10f;
        }

        public override void Interpolate(float delta, BasicMovementState stateOld, BasicMovementState stateNew)
        {
            this.rb.position = Vector3.Lerp(stateOld.position, stateNew.position, delta);
            this.rb.rotation = Quaternion.Lerp(stateOld.rotation, stateNew.rotation, delta);
        }

        public override BasicMovementInput GetCommand(int commandNumber, double time)
        {
            return new BasicMovementInput() { moveDirection = moveVector, commandNumber = commandNumber, time = time };
        }

        private void Update()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");
            this.moveVector = new Vector3(moveX, 0f, moveZ).normalized;
        }
    }
}