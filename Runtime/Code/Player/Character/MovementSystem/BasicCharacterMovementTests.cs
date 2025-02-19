using Code.Player.Character;
using UnityEngine;
using UnityEngine.InputSystem;

public class BasicCharacterMovementTests : MonoBehaviour {
    public float impulseStrength = 10;
    public AnimationClip testAnim;

    private bool loopingOn = false;
    private void Awake(){

         // Create an Action that binds to the primary action control on all devices.
        var teleportAction = new InputAction(binding: "<Keyboard>/f5");
        // Have it run your code when the Action is triggered.
        teleportAction.performed += (context)=>{
            TestTeleport();
        };
        // Start listening for control changes.
        teleportAction.Enable();

        var impulseAction = new InputAction(binding: "<Keyboard>/f6");
        impulseAction.performed += (context)=>{
            TestImpulse();
        };
        impulseAction.Enable();
        
        var flinchAction = new InputAction(binding: "<Keyboard>/f7");
        flinchAction.performed += (context)=>{
            TestFlinch();
        };
        flinchAction.Enable();
    }

    public void TestTeleport(){
        foreach (var character in GetAllCharacters()){
            Vector3 newPos = character.transform.position + character.GetLookVector().normalized * 3;
            character.TeleportAndLook(newPos, character.GetLookVector());
        }
    }

    public void TestImpulse(){
        foreach (var character in GetAllCharacters()){
            int negativeX = Random.Range(0,2) == 1 ? 1 : -1;
            int negativeZ = Random.Range(0,2) == 1 ? 1 : -1;
            float minImpulseStrength = impulseStrength/2f;
            character.AddImpulse(
                new Vector3(Random.Range(minImpulseStrength, impulseStrength) * negativeX, 
                Random.Range(minImpulseStrength,impulseStrength), 
                Random.Range(minImpulseStrength, impulseStrength) * negativeZ));
        }
    }

    public void TestFlinch(){
        foreach (var character in GetAllCharacters()){
            if(loopingOn){
                character.animationHelper.StopAnimation(CharacterAnimationHelper.CharacterAnimationLayer.OVERRIDE_1, 0.1f);
                loopingOn = false;
            }else{
                character.animationHelper.PlayAnimation(testAnim, CharacterAnimationHelper.CharacterAnimationLayer.OVERRIDE_1, 0.1f);
                loopingOn = testAnim.isLooping;
            }
        }
    }

    private CharacterMovement[] GetAllCharacters(){
        return FindObjectsByType<CharacterMovement>(FindObjectsSortMode.None);
    }
}
