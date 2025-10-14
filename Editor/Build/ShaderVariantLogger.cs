using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Editor.Build {
    public sealed class ShaderVariantLogger : IPreprocessShaders, IPreprocessBuildWithReport, IPostprocessBuildWithReport {
        // Make this logger run late so it sees the post-strip list the build is actually going to compile.
        public int callbackOrder => int.MaxValue;

        private static readonly Dictionary<string, long> ShaderVariantTotals = new();
        private static Stopwatch _buildSw;

        // Called for every shader snippet Unity is about to compile (after your strippers have run)
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data) {
            if (shader == null) return;

            // Count remaining variants for this (shader, pass, stage) after stripping.
            // string key = $"{shader.name} | {snippet.passName} | {snippet.shaderType}";
            long count = data?.Count ?? 0;

            // Aggregate per shader for a cleaner summary.
            if (!ShaderVariantTotals.ContainsKey(shader.name)) ShaderVariantTotals[shader.name] = 0;
            ShaderVariantTotals[shader.name] += count;

            // Optional: verbose line per snippet (comment out if too noisy)
            // UnityEngine.Debug.Log($"[ShaderBuild] {key} -> {count} variants");
        }

        public void OnPreprocessBuild(BuildReport report) {
            ShaderVariantTotals.Clear();
            _buildSw = Stopwatch.StartNew();

            if (report.summary.platform is BuildTarget.iOS or BuildTarget.Android) {
                CreateAssetBundles.SwapToQualityLevel("Low");
            } else {
                CreateAssetBundles.SwapToQualityLevel("Normal");
            }

            Debug.Log("[ShaderBuild] Begin build — logging post-strip shader variants…");
        }

        public void OnPostprocessBuild(BuildReport report) {
            _buildSw?.Stop();

            // Summary: top offenders first
            var summary = ShaderVariantTotals
                .OrderByDescending(kv => kv.Value)
                .Take(50) // keep output readable
                .Select(kv => $"{kv.Key}: {kv.Value:n0} variants");

            Debug.Log($"[ShaderBuild] Build finished in {_buildSw?.Elapsed.ToString() ?? "?"}. Shader variant summary:\n" +
                      string.Join("\n", summary));

            // Quick heuristic to catch “cache-cold” builds:
            long total = ShaderVariantTotals.Values.Sum();
            if (total > 100_000) {
                Debug.LogWarning($"[ShaderBuild] High total variant count detected ({total:n0}). " +
                                 "This usually means cache invalidation or stripping config changed.");
            }
        }
    }
}