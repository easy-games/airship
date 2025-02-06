using System;
using Code.Player.Character.Net;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement.BasicTest
{
    public class BasicMovementState : StateSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;

        public override string ToString()
        {
            return "lastcmd: " + this.lastProcessedCommand + " pos: " + position.ToString() + " rot: " +
                   rotation.ToString() + " vel: " + this.velocity + " angVel: " + this.angularVelocity;
        }

        public override bool CompareWithMargin(float margin, StateSnapshot snapshot)
        {
            if (snapshot is not BasicMovementState other) return false;
            return this.lastProcessedCommand == other.lastProcessedCommand && this.position == other.position &&
                   this.rotation == other.rotation;
        }
    }
}