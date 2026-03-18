using Code.Network.StateSystem.Structures;
using Code.Player.Character.Net;
using UnityEngine;

namespace Code.Network.StateSystem.Implementations.TestMovementSystem
{
    public struct TestMovementState : IStateSnapshot
    {
        public int lastProcessedCommand { get; set; }
        public double time { get; set; }
        public int tick { get; set; }
        
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public int jumpTicksUntil;

        public override string ToString()
        {
            return "lastcmd: " + this.lastProcessedCommand + " pos: " + position.ToString() + " rot: " +
                   rotation.ToString() + " vel: " + this.velocity + " angVel: " + this.angularVelocity + " tick: " +
                   this.tick;
        }

        public StateDiff CreateDiff<TState>(TState snapshot) where TState : IStateSnapshot {
            throw new System.NotImplementedException();
        }

        public IStateSnapshot ApplyDiff(StateDiff diff) {
            throw new System.NotImplementedException();
        }

        public bool Compare<TSystem, TState, TDiff, TInput>(NetworkedStateSystem<TSystem, TState, TDiff, TInput> system, TState snapshot)
            where TState : struct, IStateSnapshot
            where TDiff : StateDiff
            where TInput : InputCommand
            where TSystem : NetworkedStateSystem<TSystem, TState, TDiff, TInput>
        {
            if (snapshot is not TestMovementState other) return false;
            return this.lastProcessedCommand == other.lastProcessedCommand && this.position == other.position &&
                   this.rotation == other.rotation;
        }

        public object Clone()
        {
            return new TestMovementState()
            {
                tick = tick,
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