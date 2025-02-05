using System;
using Code.Player.Character.Net;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement.BasicTest
{
    public class BasicMovementState : StateSnapshot, IEquatable<BasicMovementState>
    {
        public Vector3 position;
        public Quaternion rotation;

        public override string ToString()
        {
            return position.ToString() + " " + rotation.ToString();
        }

        public bool Equals(BasicMovementState other)
        {
            return this.position == other.position && this.rotation == other.rotation;
        }
    }
    
    
}