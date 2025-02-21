using Code.Player.Character.Net;
using UnityEngine;

namespace Code.Network.StateSystem.Implementations.TestMovementSystem
{
    public class TestMovementInput : InputCommand
    {
        public Vector3 moveDirection;
        public bool jump;
        
        public override string ToString()
        {
            return "cmd: " + this.commandNumber + " mov: " + this.moveDirection.ToString();
        }
    }
}