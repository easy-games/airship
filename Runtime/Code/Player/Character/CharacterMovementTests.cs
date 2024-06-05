using Code.Player.Character;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterMovementTests : MonoBehaviour {
    public float impulseStrength = 10;
    private void Awake(){

         // Create an Action that binds to the primary action control on all devices.
        var teleportAction = new InputAction(binding: "<Keyboard>/t");

        // Have it run your code when the Action is triggered.
        teleportAction.performed += (context)=>{
            TestTeleport();
        };

        // Start listening for control changes.
        teleportAction.Enable();

         // Create an Action that binds to the primary action control on all devices.
        var impulseAction = new InputAction(binding: "<Keyboard>/i");

        // Have it run your code when the Action is triggered.
        impulseAction.performed += (context)=>{
            TestImpulse();
        };

        // Start listening for control changes.
        impulseAction.Enable();
    }

    public void TestTeleport(){
        foreach (var character in GetAllCharacters()){
            character.TeleportAndLook(character.transform.position + character.transform.forward * 5, character.GetLookVector());
        }
    }

    public void TestImpulse(){
        foreach (var character in GetAllCharacters()){
            int negativeX = Random.Range(0,2) == 1 ? 1 : -1;
            int negativeZ = Random.Range(0,2) == 1 ? 1 : -1;
            float minImpulseStrength = impulseStrength/2f;
            character.ApplyImpulse(
                new Vector3(Random.Range(minImpulseStrength, impulseStrength) * negativeX, 
                Random.Range(minImpulseStrength,impulseStrength), 
                Random.Range(minImpulseStrength, impulseStrength) * negativeZ));
        }
    }

    private CharacterMovement[] GetAllCharacters(){
        return FindObjectsByType<CharacterMovement>(FindObjectsSortMode.None);
    }
}
