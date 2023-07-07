using UnityEngine;

[ExecuteInEditMode]
public class PhysicsSetup : MonoBehaviour
{
    private void Start()
    {
        PhysicsLayerEditor.CreateLayer("Character");
        PhysicsLayerEditor.CreateLayer("Block");
        PhysicsLayerEditor.CreateLayer("BridgeAssist");
        PhysicsLayerEditor.CreateLayer("GroundItem");
        PhysicsLayerEditor.CreateLayer("FirstPerson");
        PhysicsLayerEditor.CreateLayer("Projectile");
    }
}