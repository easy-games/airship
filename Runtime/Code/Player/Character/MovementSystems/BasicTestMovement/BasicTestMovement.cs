using System;
using Code.Player.Character.Net;
using Mirror;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement.BasicTest
{
    public class BasicTestMovement: NetworkedMovement<BasicMovementState, BasicMovementInput>
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
            Debug.Log("setting state to" + state.lastProcessedCommand + " type" + state.GetType());
            var s = state as BasicMovementState;
            Debug.Log("state now "+ s.lastProcessedCommand);
            rb.MovePosition(s.position);
            rb.MoveRotation(s.rotation);
        }

        public override BasicMovementState GetCurrentState(int commandNumber, double time)
        {
            return new BasicMovementState() { position = rb.position, rotation = rb.rotation, lastProcessedCommand = commandNumber, time = time};
        }

        public override void Tick(BasicMovementInput command, bool replay)
        {
            Debug.Log("Ticking command" + command.commandNumber);
            var c = command as BasicMovementInput;
            rb.MovePosition(rb.position + c.moveDirection * Time.fixedDeltaTime * 5f);
        }

        public override void Interpolate(float delta, BasicMovementState stateOld, BasicMovementState stateNew)
        {
            print("Interpolating with delta: " + delta);
            this.rb.position = Vector3.Lerp(stateOld.position, stateNew.position, delta);
            this.rb.rotation = Quaternion.Lerp(stateOld.rotation, stateNew.rotation, delta);
        }

        public override BasicMovementInput GetCommand(int commandNumber)
        {
            Debug.Log("retrieving command" + moveVector);
            return new BasicMovementInput() { moveDirection = moveVector, commandNumber = commandNumber};
        }

        private void Update()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");  
            this.moveVector = new Vector3(moveX, 0f, moveZ).normalized;
        }
    }
}