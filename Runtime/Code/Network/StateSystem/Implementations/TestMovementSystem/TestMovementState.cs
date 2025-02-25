using Code.Network.StateSystem.Structures;
using Unity.VisualScripting;
using UnityEngine;

namespace Code.Network.StateSystem.Implementations.TestMovementSystem
{
    public class TestMovementState : StateSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public int jumpTicksUntil;

        public override string ToString()
        {
            return "lastcmd: " + this.lastProcessedCommand + " pos: " + position.ToString() + " rot: " +
                   rotation.ToString() + " vel: " + this.velocity + " angVel: " + this.angularVelocity + " time: " +
                   this.time;
        }

        public override bool CompareWithMargin(float margin, StateSnapshot snapshot)
        {
            if (snapshot is not TestMovementState other) return false;
            return this.lastProcessedCommand == other.lastProcessedCommand && this.position == other.position &&
                   this.rotation == other.rotation;
        }

        public override object Clone()
        {
            return new TestMovementState()
            {
                time = time,
                lastProcessedCommand = lastProcessedCommand,
                position = position,
                rotation = rotation,
                velocity = velocity,
                angularVelocity = angularVelocity,
                jumpTicksUntil = jumpTicksUntil
            };
        }
    }
}