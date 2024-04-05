using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

public class ImageImportSettings : AssetPostprocessor
{
    // Specify the root folder or subfolder where the image should be detected
    private const string TargetFolder = "Assets/Bundles";

    //Lowercase!
    private string[] SubFolderContains = new string[]
    {
        "voxelworld"
    };

    // Specify the default settings for the image
    private const TextureImporterCompression DefaultCompression = TextureImporterCompression.Compressed;
    private const bool DefaultGenerateMipMaps = true;

    // Method called when an asset is imported
    private void OnPreprocessTexture()
    {
        // Check if the asset is in the target folder or its subfolders
        if (assetPath.StartsWith(TargetFolder) == false)
        {
            return;
        }

        bool found = false;
        string path = Path.GetDirectoryName(assetPath);
        foreach (string str in SubFolderContains)
        {
            //Get the assetPath filepath without the filename
            if (path.ToLower().Contains(str))
            {
                found = true;
            }
        }
        if (found == false)
        {
            return;
        }

        // Get the texture importer for the asset
        TextureImporter importer = assetImporter as TextureImporter;
 
        //Normal maps get set by _n
        if (Path.GetFileNameWithoutExtension(assetPath).EndsWith("_n"))
        {
            importer.textureType = TextureImporterType.NormalMap;
        }

        //Default settings for all images in voxelWorld etc
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.sRGBTexture = false;

        //Check their name for contains "roughness" or "metallic", if so those are not meant to be sRGB = false

        bool dataChannel = false;
        if (Path.GetFileNameWithoutExtension(assetPath).ToLower().Contains("roughness") || Path.GetFileNameWithoutExtension(assetPath).ToLower().Contains("metallic"))
        {
            dataChannel = true;
        }
        //If they end with _r or _m, also a data channel
        if (Path.GetFileNameWithoutExtension(assetPath).EndsWith("_r") || Path.GetFileNameWithoutExtension(assetPath).EndsWith("_m"))
        {
            dataChannel = true;
        }

        if (dataChannel)
        {
            //Do nothing atm
            //importer.sRGBTexture = true;
        }


    }

    void OnPreprocessCubemap(Cubemap Texture) {

        // Get the texture importer for the asset
        TextureImporter importer = assetImporter as TextureImporter;
        
        TextureImporterSettings src = new TextureImporterSettings();
        src.filterMode = FilterMode.Trilinear;
        src.mipmapEnabled = true;
        src.cubemapConvolution = TextureImporterCubemapConvolution.Specular;
        src.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
        src.sRGBTexture = false;
        importer.SetTextureSettings(src);
    }

}

