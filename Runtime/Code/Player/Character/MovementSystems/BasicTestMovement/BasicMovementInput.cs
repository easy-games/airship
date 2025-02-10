using Code.Player.Character.Net;
using UnityEngine;

namespace Code.Player.Character.NetworkedMovement.BasicTest
{
    public class BasicMovementInput : InputCommand
    {
        public Vector3 moveDirection;
        public bool jump;
        
        public override string ToString()
        {
            return "cmd: " + this.commandNumber + " mov: " + this.moveDirection.ToString();
        }
    }
}