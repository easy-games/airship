using System;
using UnityEngine;
using UnityEngine.Rendering;

public class FixDebugManager : MonoBehaviour
{
    private void Awake()
    {
        DebugManager.instance.enableRuntimeUI = false;
    }
}