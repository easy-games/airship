using System;
using Code.Player.Character;
using Code.Player.Human;
using UnityEngine;
using UnityEngine.Serialization;

public class EditorCharacterMovementControls : MonoBehaviour {
    [FormerlySerializedAs("entityDriver")] public CharacterMovement characterMovement;

    private void Start() {
        this.characterMovement = GetComponent<CharacterMovement>();
    }

    private void Update() {
        var w = Input.GetKey(KeyCode.W);
        var s = Input.GetKey(KeyCode.S);
        var a = Input.GetKey(KeyCode.A);
        var d = Input.GetKey(KeyCode.D);

        var forward = w == s ? 0 : w ? 1 : -1;
        var sideways = d == a ? 0 : d ? 1 : -1;

        var moveDirection = new Vector3(sideways, 0, forward);
        characterMovement.SetMoveInput(moveDirection, Input.GetKey(KeyCode.Space), Input.GetKey(KeyCode.LeftShift), Input.GetKey(KeyCode.C), false);
        print(moveDirection);
    }
}