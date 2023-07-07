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
        PhysicsLayerEditor.CreateLayer("Character");
        PhysicsLayerEditor.CreateLayer("Block");
        PhysicsLayerEditor.CreateLayer("BridgeAssist");
        PhysicsLayerEditor.CreateLayer("GroundItem");
        PhysicsLayerEditor.CreateLayer("FirstPerson");
        PhysicsLayerEditor.CreateLayer("Projectile");
        PhysicsLayerEditor.CreateLayer("IgnoreCollide");

        layers = new List<int>();
        layers.Add(LayerMask.NameToLayer("Character"));
        layers.Add(LayerMask.NameToLayer("Projectile"));
        layers.Add(LayerMask.NameToLayer("Block"));
        layers.Add(LayerMask.NameToLayer("BridgeAssist"));
        layers.Add(LayerMask.NameToLayer("GroundItem"));
        layers.Add(LayerMask.NameToLayer("FirstPerson"));
        layers.Add(LayerMask.NameToLayer("IgnoreCollide"));

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("Character"), true);
        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Character"), LayerMask.NameToLayer("GroundItem"), true);

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Projectile"), LayerMask.NameToLayer("Projectile"), true);

        Physics.IgnoreLayerCollision(LayerMask.NameToLayer("Block"), LayerMask.NameToLayer("Block"), true);

        IgnoreAllLayers(LayerMask.NameToLayer("BridgeAssist"));

        IgnoreAllLayers(LayerMask.NameToLayer("IgnoreCollide"));
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