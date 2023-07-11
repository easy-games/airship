using System;
using UnityEngine;

public class VoxelWorldPositionIndicator : MonoBehaviour
{
    [SerializeField, Tooltip("True to make the Minecraft Map Loader not replace this value.")]
    public bool doNotOverwrite = false;

    private void Start()
    {
        this.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
    }
}