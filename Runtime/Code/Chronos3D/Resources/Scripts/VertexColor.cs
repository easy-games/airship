using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[ExecuteInEditMode]
public class VertexColor : MonoBehaviour
{
    // The color to apply to the vertex colors
    public Color vertexColor = Color.white;

    private void Start()
    {
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            Mesh duplicateMesh = Instantiate(mesh);
            // Mesh duplicateMesh = new Mesh();
            // duplicateMesh.vertices = mesh.vertices;
            // duplicateMesh.triangles = mesh.triangles;
            // duplicateMesh.normals = mesh.normals;
            // duplicateMesh.tangents = mesh.tangents;
            // duplicateMesh.uv = mesh.uv;
            // duplicateMesh.uv2 = mesh.uv2;
            // duplicateMesh.uv3 = mesh.uv3;
            // duplicateMesh.uv4 = mesh.uv4;
            // duplicateMesh.colors = mesh.colors;
            // duplicateMesh.colors32 = mesh.colors32;
            meshFilter.sharedMesh = duplicateMesh;
        }
        
        ApplyVertexColor();
    }

    // Apply the vertex color to any attached meshes
    private void ApplyVertexColor()
    {
        // Get all the mesh filters attached to this object
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        SkinnedMeshRenderer[] skinnedMeshRenderers = GetComponentsInChildren<SkinnedMeshRenderer>();

        foreach (SkinnedMeshRenderer smr in skinnedMeshRenderers) {
            Mesh mesh = smr.sharedMesh;
            if (!mesh) continue;

            Color32[] colors = new Color32[mesh.vertexCount];
            for (int i = 0; i < mesh.vertexCount; i++) {
                colors[i] = vertexColor;
            }

            mesh.colors32 = colors;
        }

        // Loop through each mesh and apply the vertex color
        foreach (MeshFilter meshFilter in meshFilters)
        {
            Mesh mesh = meshFilter.sharedMesh;
            if (!mesh) continue;
            
            if (!mesh.isReadable)
            {
                //Debug.LogError("Mesh was missing read/write: " + mesh.name + ". Part of " + gameObject.name);
                continue;
            }

            // Make sure the mesh has vertex colors
            if (!mesh.colors32.Any())
            {
                // Debug.LogWarning("Mesh " + mesh.name + " does not have vertex colors");
                //continue;
            }

            // Create an array of vertex colors, with the new color applied to each vertex
            Color32[] colors = new Color32[mesh.vertexCount];
            for (int i = 0; i < mesh.vertexCount; i++)
            {
                colors[i] = vertexColor;
            }

            // Assign the new vertex colors to the mesh
            mesh.colors32 = colors;
        }
    }

    // Called when the color is changed in the inspector
    private void OnValidate()
    {
        ApplyVertexColor();
    }
}