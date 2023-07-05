using UnityEngine;
using UnityEditor;
using System.IO;
using System.Linq;
using System;
using FishNet.Managing.Scened;
using UnityEditor.AssetImporters;
using System.Drawing.Printing;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEditor.IMGUI.Controls;

public class CubemapPickerWindow : EditorWindow
{
    
    public static void Show(Action<string> onCubemapSelected)
    {
        var window = GetWindow<CubemapPickerWindow>();
        window.onCubemapSelected = onCubemapSelected;
        window.titleContent = new GUIContent("Cubemap Picker");
        window.minSize = new Vector2(256f+16, 300f);
        window.maxSize = new Vector2(256f + 16, 600f);
        window.ShowModal();
    }

    private Vector2 scrollPosition = Vector2.zero;
    private int selectedCubemapIndex = -1;
    private List<Cubemap> cubemaps = new List<Cubemap>();
    private List<Texture2D> preview = new List<Texture2D>();
    private Action<string> onCubemapSelected;
    
    //On close, cleanup the preview textures
    private void OnDestroy()
    {
        foreach (var tex in preview)
        {
            DestroyImmediate(tex);
        }
        preview.Clear();
    }
    
    private void OnEnable()
    {
        //declare a list of Cubemap
         
        cubemaps = new();
        preview = new();

        //Check every asset bundle for cubemaps
        var assetBundleNames = AssetDatabase.GetAllAssetBundleNames();
        
        foreach (var assetBundleName in assetBundleNames)
        {
            //Check each asset in the asset bundle
            foreach (var assetPath in AssetDatabase.GetAssetPathsFromAssetBundle(assetBundleName))
            {
                //If the asset is a cubemap, add it to the list
                if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) == typeof(Cubemap))
                {
                    Cubemap asset = AssetDatabase.LoadAssetAtPath<Cubemap>(assetPath);
                    cubemaps.Add(asset);

                    Texture2D previewTexture = CubemapPreviewUtility.CreatePreview(asset, 256);
                    
                    preview.Add(previewTexture);
                }
            }
        }
        
        

        
    }
    private void OnGUI()
    {
        if (AssetPreview.IsLoadingAssetPreviews())
        {
            EditorGUILayout.LabelField("Loading...");
            Repaint();
            return;
        }
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        for (int i = 0; i < cubemaps.Count; i++)
        {
            var cubemap = cubemaps[i];
            var previewTex = preview[i];
           
            var rect = GUILayoutUtility.GetRect(256f, 256f, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                selectedCubemapIndex = i;
                Event.current.Use();
            }
     
            if (selectedCubemapIndex == i)
            {
                //make a rect the same as rect, but inset by 2 pixels, and is a square (width == height)
                var selectedRect = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.width - 4f);

                //Draw it centered
                EditorGUI.DrawRect(selectedRect, Color.yellow); // Highlight the selected item
            }

            if (previewTex != null)
            {
                //Make a preview rect the same as rect, but inset 6 pixels
                var selectedRect = new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.width - 8f);

                EditorGUI.DrawPreviewTexture(selectedRect, previewTex, null, ScaleMode.ScaleAndCrop);
    
            }
          
            GUILayout.Label(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(cubemap)), EditorStyles.centeredGreyMiniLabel);
        
        }

        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Select", GUILayout.Height(40f)))
        {
            if (selectedCubemapIndex >= 0 && selectedCubemapIndex < cubemaps.Count)
            {
                var selectedCubemap = cubemaps[selectedCubemapIndex];
                var cubemapPath = AssetDatabase.GetAssetPath(selectedCubemap);
                onCubemapSelected?.Invoke(cubemapPath);
            }

            //Cleanup the allocated cubemap preview textures
            foreach (var previewTex in preview)
            {
                DestroyImmediate(previewTex);
            }
            preview.Clear();

            Close();
        }
    }

}


 
public static class CubemapPreviewUtility
{
    public static Texture2D CreatePreview(Cubemap cubemap, int textureResolution)
    {
        // Create render texture
        RenderTexture renderTexture = new RenderTexture(textureResolution, textureResolution, 16);
        renderTexture.Create();

        // Create camera
        GameObject cameraObject = new GameObject("PreviewCamera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = Vector3.zero;
        camera.clearFlags = CameraClearFlags.Color;
        
        camera.targetTexture = renderTexture;

        // Create preview sphere
        GameObject previewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        previewSphere.transform.position = Vector3.forward;

        // Assign cubemap to a material and set it to the sphere
        Shader shader = Shader.Find("Chronos/CubemapPreviewShader");
        
        Debug.Log("Shader: " + shader);
        Material sphereMaterial = new Material(shader);
        sphereMaterial.SetTexture("_CubemapTex", cubemap);
        previewSphere.GetComponent<Renderer>().material = sphereMaterial;

        //Set the camera and sphere to a layer that is ignored by the main camera
        int previewLayer = 1;
        camera.cullingMask = 1 << previewLayer;
        previewSphere.layer = previewLayer;
        

        // Render the camera's view
        camera.Render();

        // Create Texture2D to output
        Texture2D outputTexture = new Texture2D(textureResolution, textureResolution, TextureFormat.RGBA32, false);
        RenderTexture.active = renderTexture;
        outputTexture.ReadPixels(new Rect(0, 0, textureResolution, textureResolution), 0, 0);
        outputTexture.Apply();
        RenderTexture.active = null;

        // Cleanup
        UnityEngine.Object.DestroyImmediate(previewSphere);
        UnityEngine.Object.DestroyImmediate(cameraObject);
        renderTexture.Release();

        // Return result
        return outputTexture;
    }
}
