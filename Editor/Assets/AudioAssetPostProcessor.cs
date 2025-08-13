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
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.5f,
            };
            // For clips over 30s in length we compress in memory on mobile to keep memory lower
            if (clip.length > 30) {
                var mobileQuality = new AudioImporterSampleSettings() {
                    loadType = AudioClipLoadType.CompressedInMemory,
                    compressionFormat = AudioCompressionFormat.Vorbis,
                    quality = 0.3f,
                };
                importer.SetOverrideSampleSettings(BuildTargetGroup.iOS, mobileQuality);
                importer.SetOverrideSampleSettings(BuildTargetGroup.Android, mobileQuality);
            }
            importer.defaultSampleSettings = audioImporterSampleSettings;
            processedClips.Add(importer.assetPath);
            importer.SaveAndReimport();
        }
    }
}