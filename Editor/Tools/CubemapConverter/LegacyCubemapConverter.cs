using UnityEngine;
using UnityEditor;
using System.IO;

public class LegacyCubemapConverter : EditorWindow {
    [MenuItem("Assets/Convert Legacy Cubemap To Standard", true)]
    private static bool ValidateConvertCubemap() {
        return Selection.activeObject is UnityEngine.Cubemap;
    }

    [MenuItem("Assets/Convert Legacy Cubemap To Standard")]
    private static void ConvertCubemap() {
        UnityEngine.Cubemap selectedCubemap = Selection.activeObject as UnityEngine.Cubemap;
        if (selectedCubemap == null) {
            Debug.LogError("Selected object is not a Cubemap.");
            return;
        }

        string path = AssetDatabase.GetAssetPath(Selection.activeObject);

        // Ensure the cubemap texture is readable
        MakeTextureReadable(path);

        int size = selectedCubemap.width;
        Texture2D newCubemap = new Texture2D(size, size * 6, TextureFormat.RGBA32, false);

        // Set pixels for each face in the new Cubemap
        SetCubemapPixels(selectedCubemap, newCubemap, size);

        // Save the packed cubemap as a new asset
        SaveNewCubemapAsset(path, newCubemap);

        // Set import settings for the new packed texture
        SetImportSettingsForNewAsset(path);
    }

    private static void MakeTextureReadable(string path) {


        string croppedPath = Application.dataPath;
        //Remove /assets/ from the end
        croppedPath = croppedPath.Substring(0, croppedPath.Length - 7);
        Debug.Log(croppedPath);
        string windowsPath = Path.Combine(croppedPath, path);


        Debug.Log(windowsPath);
        string content = File.ReadAllText(windowsPath);

        content = content.Replace("m_IsReadable: 0", "m_IsReadable: 1");

        File.Delete(windowsPath);

        File.WriteAllText(windowsPath, content);

        AssetDatabase.ImportAsset(path);
    }

    private static void SetCubemapPixels(UnityEngine.Cubemap selectedCubemap, Texture2D newCubemap, int size) {
        newCubemap.SetPixels(0, size * 0, size, size, FlipY(selectedCubemap.GetPixels(CubemapFace.NegativeZ), size));
        newCubemap.SetPixels(0, size * 1, size, size, FlipY(selectedCubemap.GetPixels(CubemapFace.PositiveZ), size));
        newCubemap.SetPixels(0, size * 2, size, size, FlipY(selectedCubemap.GetPixels(CubemapFace.NegativeY), size));
        newCubemap.SetPixels(0, size * 3, size, size, FlipY(selectedCubemap.GetPixels(CubemapFace.PositiveY), size));
        newCubemap.SetPixels(0, size * 4, size, size, FlipY(selectedCubemap.GetPixels(CubemapFace.NegativeX), size));
        newCubemap.SetPixels(0, size * 5, size, size, FlipY(selectedCubemap.GetPixels(CubemapFace.PositiveX), size));
        

    }

    private static Color[] FlipY(Color[] pixels, int size) {
        Color[] flippedPixels = new Color[pixels.Length];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                flippedPixels[x + y * size] = pixels[x + (size - y - 1) * size];
            }
        }
        return flippedPixels;
    }

    private static void SaveNewCubemapAsset(string path, Texture2D newCubemap) {
        byte[] bytes = newCubemap.EncodeToPNG();
        string newPath = Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + "_Packed.png";
        File.WriteAllBytes(newPath, bytes);
        AssetDatabase.Refresh();
    }

    private static void SetImportSettingsForNewAsset(string path) {
        string newPath = Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + "_Packed.png";
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(newPath);
        importer.textureShape = TextureImporterShape.TextureCube;
        importer.sRGBTexture = false;
        importer.SaveAndReimport();
    }
}
