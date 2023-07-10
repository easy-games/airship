using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEngine;
#endif

public static class PhysicsSetup
{
    private static List<int> layers;

    public static void Setup()
    {
#if UNITY_EDITOR
        PhysicsLayerEditor.SetLayer(3, "Character");
        PhysicsLayerEditor.SetLayer(6, "Block");
        PhysicsLayerEditor.SetLayer(7, "BridgeAssist");
        PhysicsLayerEditor.SetLayer(8, "IgnoreCollide");
        PhysicsLayerEditor.SetLayer(9, "GroundItem");
        PhysicsLayerEditor.SetLayer(10, "FirstPerson");
        PhysicsLayerEditor.SetLayer(11, "Projectile");

        layers = new List<int>();
        layers.Add(LayerMask.NameToLayer("Default"));
        layers.Add(LayerMask.NameToLayer("TransparentFX"));
        layers.Add(LayerMask.NameToLayer("Ignore Raycast"));
        layers.Add(LayerMask.NameToLayer("Character"));
        layers.Add(LayerMask.NameToLayer("Water"));
        layers.Add(LayerMask.NameToLayer("UI"));
        layers.Add(LayerMask.NameToLayer("Block"));
        layers.Add(LayerMask.NameToLayer("BridgeAssist"));
        layers.Add(LayerMask.NameToLayer("IgnoreCollide"));
        layers.Add(LayerMask.NameToLayer("GroundItem"));
        layers.Add(LayerMask.NameToLayer("FirstPerson"));
        layers.Add(LayerMask.NameToLayer("Projectile"));

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Character"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("GroundItem"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Projectile"), false);

        IgnoreAllLayers(LayerMask.NameToLayer("BridgeAssist"));
        IgnoreAllLayers(LayerMask.NameToLayer("IgnoreCollide"));

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Projectile"), LayerMask.NameToLayer("Projectile"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Projectile"), LayerMask.NameToLayer("FirstPerson"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Projectile"), LayerMask.NameToLayer("GroundItem"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Projectile"), LayerMask.NameToLayer("BridgeAssist"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Projectile"), LayerMask.NameToLayer("UI"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Projectile"), LayerMask.NameToLayer("Water"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Projectile"), LayerMask.NameToLayer("Ignore Raycast"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Projectile"), LayerMask.NameToLayer("TransparentFX"), true);

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Block"), LayerMask.NameToLayer("Block"), true);

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("FirstPerson"), LayerMask.NameToLayer("Character"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("FirstPerson"), LayerMask.NameToLayer("FirstPerson"), true);
#endif
    }

    public static void IgnoreAllLayers(int layer)
    {
        foreach (var otherLayer in layers)
        {
#if UNITY_EDITOR
            Physics.IgnoreLayerCollision(layer, otherLayer, true);
#endif
        }
    }
}