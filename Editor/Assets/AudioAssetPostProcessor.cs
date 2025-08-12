using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Editor.Assets {
    public class AudioAssetPostProcessor : AssetPostprocessor {
        private static HashSet<string> processedClips = new HashSet<string>();
        
        private void OnPostprocessAudio(AudioClip clip) {
            var importer = (AudioImporter) assetImporter;
            if (processedClips.Contains(importer.assetPath)) return; // Already processed
                
            importer.loadInBackground = true;
            
            var audioImporterSampleSettings = new AudioImporterSampleSettings() {
                loadType = AudioClipLoadType.CompressedInMemory,
                compressionFormat = AudioCompressionFormat.ADPCM,
            };
            if (clip.length > 30) {
                audioImporterSampleSettings = new AudioImporterSampleSettings() {
                    loadType = AudioClipLoadType.Streaming,
                    compressionFormat = AudioCompressionFormat.ADPCM,
                };
            }
            importer.SetOverrideSampleSettings(BuildTargetGroup.iOS, audioImporterSampleSettings);
            processedClips.Add(importer.assetPath);
            importer.SaveAndReimport();
        }
    }
}