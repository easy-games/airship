using UnityEngine;

namespace Code.Player.Character.NetworkedMovement.BasicTest
{
    public class BasicTestMovement : NetworkedMovement<BasicMovementState, BasicMovementInput>
    {
        private Rigidbody rb;
        private Vector3 moveVector;
        private bool jump;
        private int jumpTicksUntil = 0;

        private void Awake()
        {
            rb = this.GetComponent<Rigidbody>();
        }

        public override void OnSetMode(MovementMode mode)
        {
            if (mode == MovementMode.Observer)
            {
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (mode == MovementMode.Authority || mode == MovementMode.Input)
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        public override void SetCurrentState(BasicMovementState state)
        {
            // Debug.Log("Setting state to match: " + state);
            jumpTicksUntil = state.jumpTicksUntil;
            rb.position = state.position;
            rb.rotation = state.rotation;
            if (!rb.isKinematic)
            {
                // Debug.Log("Updating velocities on non-kinematic rigidbody");
                rb.velocity = state.velocity;
                rb.angularVelocity = state.angularVelocity;
            }
        }

        public override BasicMovementState GetCurrentState(int commandNumber, double time)
        {
            return new BasicMovementState()
            {
                position = rb.position, rotation = rb.rotation, velocity = rb.velocity,
                angularVelocity = rb.angularVelocity, lastProcessedCommand = commandNumber, time = time, jumpTicksUntil = jumpTicksUntil
            };
        }

        public override void Tick(BasicMovementInput command, bool replay)
        {
            if (command == null) return;
            //rb.MovePosition(rb.position + command.moveDirection * Time.fixedDeltaTime * 10f);
            //rb.position = rb.position + command.moveDirection * Time.fixedDeltaTime * 10f;

            // Good for slow small desync checking
            // rb.velocity = command.moveDirection * 5f;
            rb.velocity = new Vector3(command.moveDirection.x * 5f, rb.velocity.y, command.moveDirection.z * 5f);

            if (command.jump && this.jumpTicksUntil == 0)
            {
                rb.AddForce(new Vector3(0, 10f, 0), ForceMode.Impulse);
                jumpTicksUntil = 10;
            }

            if (jumpTicksUntil > 0) jumpTicksUntil--;
        }

        public override void Interpolate(float delta, BasicMovementState stateOld, BasicMovementState stateNew)
        {
            this.rb.position = Vector3.Lerp(stateOld.position, stateNew.position, delta);
            this.rb.rotation = Quaternion.Lerp(stateOld.rotation, stateNew.rotation, delta);
        }

        public override void InterpolateReachedState(BasicMovementState state)
        {
            // Noop
        }

        public override BasicMovementInput GetCommand(int commandNumber, double time)
        {
            var command = new BasicMovementInput() { moveDirection = moveVector, commandNumber = commandNumber, time = time, jump = jump};
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