using UnityEngine;

namespace Code.Network.StateSystem.Implementations.TestMovementSystem
{
    public class TestMovement : NetworkedStateSystem<TestMovement, TestMovementState, TestMovementDiff, TestMovementInput>
    {
        private Rigidbody rb;
        private Vector3 moveVector;
        private bool jump;
        private int jumpTicksUntil = 0;

        private void Awake()
        {
            rb = this.GetComponent<Rigidbody>();
        }

        public override void SetMode(NetworkedStateSystemMode mode)
        {
            if (mode == NetworkedStateSystemMode.Observer)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (mode == NetworkedStateSystemMode.Authority || mode == NetworkedStateSystemMode.Input)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        public override void SetCurrentState(TestMovementState state)
        {
            // Debug.Log("Setting state to match: " + state);
            jumpTicksUntil = state.jumpTicksUntil;
            rb.position = state.position;
            rb.rotation = state.rotation;
            if (!rb.isKinematic)
            {
                // Debug.Log("Updating velocities on non-kinematic rigidbody");
                rb.linearVelocity = state.velocity;
                rb.angularVelocity = state.angularVelocity;
            }
        }

        public override TestMovementState GetCurrentState(int commandNumber, double time)
        {
            return new TestMovementState()
            {
                position = rb.position, rotation = rb.rotation, velocity = rb.linearVelocity,
                angularVelocity = rb.angularVelocity, lastProcessedCommand = commandNumber, time = time, jumpTicksUntil = jumpTicksUntil
            };
        }

        public override void Tick(TestMovementInput command, double time, bool replay)
        {
            if (command == null) return;
            //rb.MovePosition(rb.position + command.moveDirection * Time.fixedDeltaTime * 10f);
            //rb.position = rb.position + command.moveDirection * Time.fixedDeltaTime * 10f;

            // Good for slow small desync checking
            // rb.velocity = command.moveDirection * 5f;
            rb.linearVelocity = new Vector3(command.moveDirection.x * 5f, rb.linearVelocity.y, command.moveDirection.z * 5f);

            if (command.jump && this.jumpTicksUntil == 0)
            {
                rb.AddForce(new Vector3(0, 10f, 0), ForceMode.Impulse);
                jumpTicksUntil = 10;
            }

            if (jumpTicksUntil > 0) jumpTicksUntil--;
        }

        public override void Interpolate(float delta, TestMovementState stateOld, TestMovementState stateNew)
        {
            this.rb.position = Vector3.Lerp(stateOld.position, stateNew.position, delta);
            this.rb.rotation = Quaternion.Lerp(stateOld.rotation, stateNew.rotation, delta);
        }

        public override void InterpolateReachedState(TestMovementState state)
        {
            // Noop
        }

        public override TestMovementInput GetCommand(int commandNumber, double time)
        {
            var command = new TestMovementInput() { moveDirection = moveVector, commandNumber = commandNumber, jump = jump};
            jump = false;
            return command;
        }

        private void Update()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveZ = Input.GetAxisRaw("Vertical");
            this.moveVector = new Vector3(moveX, 0f, moveZ).normalized;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                this.jump = true;
            }
        }
    }
}