using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System.Collections.Generic;
using System;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Profiling;

namespace Airship {
    [LuauAPI]
    public static class AirshipRenderPipelineStatistics {
        public static int numPasses = 0;
        public static int numWorldTriangles = 0;
        public static int numMeshRenderers = 0;
        public static int numShadowCasters = 0;
        public static int numSkinnedMeshRenderers = 0;
        public static int numSkinnedTriangles = 0;
        public static int numTriangles = 0;
        public static int numVisibleSkinnedMeshRenderers = 0;
        public static int numVisibleMeshRenderers = 0;

        public static bool captureRenderingStats = false;
        public static void CaptureRenderingStats() {
            captureRenderingStats = true;
        }

        public static void Reset() {
            numMeshRenderers = 0;
            numShadowCasters = 0;
            numSkinnedMeshRenderers = 0;
            numSkinnedTriangles = 0;
            numTriangles = 0;
            numVisibleSkinnedMeshRenderers = 0;
            numVisibleMeshRenderers = 0;
            numPasses = 0;
        }

        public static void Print() {

            Debug.Log("numMeshRenderers: " + numMeshRenderers);
            Debug.Log("numShadowCasters: " + numShadowCasters);
            Debug.Log("numSkinnedMeshRenderers: " + numSkinnedMeshRenderers);
            Debug.Log("numSkinnedTriangles: " + numSkinnedTriangles);
            Debug.Log("numTriangles: " + numTriangles);
            Debug.Log("numVisibleSkinnedMeshRenderers: " + numVisibleSkinnedMeshRenderers);
            Debug.Log("numVisibleMeshRenderers: " + numVisibleMeshRenderers);
        }

        public static void ExtractStatsFromScene() {
            if (captureRenderingStats == false) {
                return;
            }

            Profiler.BeginSample("ExtractStatsFromScene");
            Reset();

            MeshRenderer[] meshRenderers = GameObject.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            SkinnedMeshRenderer[] skinnedMeshRenderers = GameObject.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None);

            for (int i = 0; i < meshRenderers.Length; i++) {
                if (meshRenderers[i].enabled == false) {
                    continue;
                }

                numMeshRenderers++;
                MeshFilter meshFilter = meshRenderers[i].GetComponent<MeshFilter>();
                if (meshFilter) {
                    numTriangles += meshFilter.sharedMesh.triangles.Length / 3;
                }
                if (meshRenderers[i].isVisible == true) {
                    numVisibleMeshRenderers++;
                }
                if (meshRenderers[i].shadowCastingMode != ShadowCastingMode.Off) {
                    numShadowCasters++;
                }
            }

            //Do skinned meshes now
            for (int i = 0; i < skinnedMeshRenderers.Length; i++) {
                if (skinnedMeshRenderers[i].enabled == false) {
                    continue;
                }

                numSkinnedMeshRenderers++;

                if (skinnedMeshRenderers[i].sharedMesh) {
                    numSkinnedTriangles += skinnedMeshRenderers[i].sharedMesh.triangles.Length / 3;
                }
                if (skinnedMeshRenderers[i].isVisible == true) {
                    numVisibleSkinnedMeshRenderers++;
                }

                if (skinnedMeshRenderers[i].shadowCastingMode != ShadowCastingMode.Off) {
                    numShadowCasters++;
                }
            }

            //Debug
            //Print();

            captureRenderingStats = false;
            Profiler.EndSample();
        }
    }
}