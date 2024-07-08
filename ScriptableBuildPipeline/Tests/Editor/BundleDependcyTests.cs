using UnityEngine;
using UnityEngine.Build.Pipeline;
using Unity.ScriptableBuildPipelineTests;
using NUnit.Framework;
using System.IO;
using System.Linq;

namespace UnityEditor.Build.Pipeline.Tests
{
    [TestFixture]
    class BundleDependencyTests
    {
        const string k_TmpAssetPath = "Assets/TempAssets";
        const string k_BuildFolder = "TestBuild";
        const int k_CntPrefabChain = 5;

        [OneTimeSetUp]
        public void Setup()
        {
            Directory.CreateDirectory(k_TmpAssetPath);

            // Create scenario similar to BPSBP-740

            var prefabRoots = new GameObject[k_CntPrefabChain];

            for (int i = k_CntPrefabChain - 1; i >= 0; i--)
            {
                var gameObject = new GameObject();
                var mb = gameObject.AddComponent<MonoBehaviourWithReference>();
                var prefabPath = $"{k_TmpAssetPath}/prefab{i}.prefab";


                if (i != k_CntPrefabChain - 1)
                {
                    // Point to the next prefab in the chain
                    mb.Reference = prefabRoots[i+1];
                    Assert.IsNotNull(mb.Reference);
                }
                else
                {
                    mb.Reference = gameObject; // Pointer to self, like in the original repro
                }

                prefabRoots[i] = PrefabUtility.SaveAsPrefabAsset(gameObject, prefabPath);
                AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport & ImportAssetOptions.ForceUpdate);
            }
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
            if (Directory.Exists(k_BuildFolder))
                Directory.Delete(k_BuildFolder, true);

            AssetDatabase.DeleteAsset(k_TmpAssetPath);
        }

        static CompatibilityAssetBundleManifest BuildPrefabBundles()
        {
            // Put each prefab into its own AssetBundle
            var bundleDefinitions = new AssetBundleBuild[k_CntPrefabChain];
            for (int i = 0; i < bundleDefinitions.Length; i++)
            {
                bundleDefinitions[i].assetBundleName = $"{i}";
                bundleDefinitions[i].assetNames = new string[] { $"{k_TmpAssetPath}/prefab{i}.prefab" };
            };

            if (Directory.Exists(k_BuildFolder))
                Directory.Delete(k_BuildFolder, true);

            Directory.CreateDirectory(k_BuildFolder);

            // Todo, confirm that the NonRecursive Mode is enabled, the test assumes that it is and i think that is the default but its not exposed in this API

            var manifest = CompatibilityBuildPipeline.BuildAssetBundles(
                k_BuildFolder,
                bundleDefinitions,
                BuildAssetBundleOptions.AppendHashToAssetBundleName,
                EditorUserBuildSettings.activeBuildTarget);

            Assert.IsNotNull(manifest);

            return manifest;
        }

#if UNITY_2023_2_OR_NEWER
        [Test, Description("BPSBP-736")]
        public void BundeHashChanges_WhenDirectDependencyChanges()
        {
            CompatibilityAssetBundleManifest manifest = BuildPrefabBundles();

            //var outputFiles = Directory.EnumerateFiles(k_BuildFolder, "*", SearchOption.TopDirectoryOnly);
            //Debug.Log("Output of the build:\n\t" + string.Join("\n\t", outputFiles));

            var outputPaths = new string[k_CntPrefabChain];

            for (int i = 0; i < k_CntPrefabChain; i++)
            {
                //e.g. a path like "TestBuild\0_135e9091b30805539e5f5f349375cd11"
                outputPaths[i] = Directory.EnumerateFiles(k_BuildFolder, $"{i}_*", SearchOption.TopDirectoryOnly).ToArray()[0];
            }

            // Change bundle 3, e.g. remove its dependency on bundle 4
            SetPrefabReferenceToNull(3);

            CompatibilityAssetBundleManifest manifest2 = BuildPrefabBundles();

            var rebuildPaths = new string[k_CntPrefabChain];
            for (int i = 0; i < k_CntPrefabChain; i++)
            {
                rebuildPaths[i] = Directory.EnumerateFiles(k_BuildFolder, $"{i}_*", SearchOption.TopDirectoryOnly).ToArray()[0];
            }

            Assert.AreEqual(outputPaths[0], rebuildPaths[0], "Bundle hash changed");
            Assert.AreEqual(outputPaths[1], rebuildPaths[1], "Bundle hash changed");

            Assert.AreNotEqual(outputPaths[2], rebuildPaths[2]); //Direct dependency changed
            Assert.AreNotEqual(outputPaths[3], rebuildPaths[3]); // We changed this bundle

            Assert.AreEqual(outputPaths[4], rebuildPaths[4], "Bundle hash changed");

            ResetPrefabReference(3);
        }

        [Test, Description("BPSBP-736")]
        public void BundleHashDoesNotChange_IfListOfReferencedBundlesDoesNotChange()
        {
            CompatibilityAssetBundleManifest manifest = BuildPrefabBundles();

            //var outputFiles = Directory.EnumerateFiles(k_BuildFolder, "*", SearchOption.TopDirectoryOnly);
            //Debug.Log("Output of the build:\n\t" + string.Join("\n\t", outputFiles));

            var outputPaths = new string[k_CntPrefabChain];

            for (int i = 0; i < k_CntPrefabChain; i++)
            {
                //e.g. a path like "TestBuild\0_135e9091b30805539e5f5f349375cd11"
                outputPaths[i] = Directory.EnumerateFiles(k_BuildFolder, $"{i}_*", SearchOption.TopDirectoryOnly).ToArray()[0];
            }

            // Change bundle 3, e.g. remove its dependency on bundle 4
            AddToTransformValues(3);

            CompatibilityAssetBundleManifest manifest2 = BuildPrefabBundles();

            var rebuildPaths = new string[k_CntPrefabChain];
            for (int i = 0; i < k_CntPrefabChain; i++)
            {
                rebuildPaths[i] = Directory.EnumerateFiles(k_BuildFolder, $"{i}_*", SearchOption.TopDirectoryOnly).ToArray()[0];
            }

            Assert.AreEqual(outputPaths[0], rebuildPaths[0], "Bundle hash changed");
            Assert.AreEqual(outputPaths[1], rebuildPaths[1], "Bundle hash changed");
            Assert.AreEqual(outputPaths[2], rebuildPaths[2], "Bundle hash changed");

            Assert.AreNotEqual(outputPaths[3], rebuildPaths[3]); // We changed this bundle

            Assert.AreEqual(outputPaths[4], rebuildPaths[4], "Bundle hash changed");
        }

        static void SetPrefabReferenceToNull(int prefabIndex)
        {
            string prefabPath = $"{k_TmpAssetPath}/prefab{prefabIndex}.prefab";
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            var monoBehaviour = prefabRoot.GetComponent<MonoBehaviourWithReference>();
            monoBehaviour.Reference = null;

            PrefabUtility.SavePrefabAsset(prefabRoot);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport & ImportAssetOptions.ForceUpdate);
        }

        static void ResetPrefabReference(int prefabIndex)
        {
            string prefabPath = $"{k_TmpAssetPath}/prefab{prefabIndex}.prefab";
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            var monoBehaviour = prefabRoot.GetComponent<MonoBehaviourWithReference>();
            monoBehaviour.Reference = prefabRoot;

            PrefabUtility.SavePrefabAsset(prefabRoot);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport & ImportAssetOptions.ForceUpdate);
        }

        static void AddToTransformValues(int prefabIndex)
        {
            string prefabPath = $"{k_TmpAssetPath}/prefab{prefabIndex}.prefab";
            GameObject prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            var transform = prefabRoot.GetComponent<Transform>();
            transform.position += new Vector3(1, 1, 1);

            PrefabUtility.SavePrefabAsset(prefabRoot);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceSynchronousImport & ImportAssetOptions.ForceUpdate);
        }
#endif

        [Ignore("BPSBP-737 / ADDR-3262")]
        [Test, Description("BPSBP-737 / ADDR-3262")]
        public void MonoScriptsAreNotNullInChainedBundles()
        {
            // Note: Test could also do variations, with MonoScript bundle enabled, maybe also NonRecursive=false
            CompatibilityAssetBundleManifest manifest = BuildPrefabBundles();

            string prefabBundleMatch = "*_*"; // Match bundle names like 0_f5b4234bbd5a5a599bd740802cc6f9cf and ignore other build output

            // All the prefabs as loaded from the assetbundles should have valid MonoBehaviour with non-null reference, matching
            // how we created them in the project.
            var builtBundlePaths = Directory.EnumerateFiles(k_BuildFolder, prefabBundleMatch, SearchOption.TopDirectoryOnly).ToArray();
            LoadBundlesAndCheckMonoScript(builtBundlePaths);
        }

        static void LoadBundlesAndCheckMonoScript(string[] bundleNames)
        {
            //Debug.Log("Loading " + string.Join("\n", bundleNames));

            var bundleCount = bundleNames.Length;
            var bundles = new AssetBundle[bundleCount];
            for (int i = 0; i < bundleCount; i++)
            {
                bundles[i] = AssetBundle.LoadFromFile(bundleNames[i]);
            }

            try
            {
                for (int i = 0; i < bundleCount; i++)
                {
                    var prefab = bundles[i].LoadAllAssets<GameObject>()[0];
                    var monoBehaviour = prefab.GetComponent<MonoBehaviourWithReference>();

                    //TEMPORARY Assert disabled until BPSBP-737 fixed
                    //Assert.IsNotNull(monoBehaviour, "Missing MonoScript or MonoBehaviourWithReference on " + bundleNames[i]);
                    //

                    if (monoBehaviour == null)
                    {
                        // Missing monoscript will result in the entire MonoBehaviour not being loaded
                        Debug.Log("Missing MonoScript or MonoBehaviourWithReference on " + bundleNames[i]);
                        continue;
                    }

                    var monoScript = MonoScript.FromMonoBehaviour(monoBehaviour);
                    Assert.IsNotNull(monoScript);
                }
            }
            finally
            {
                for (int i = 0; i < bundleCount; i++)
                    bundles[i].Unload(true);
            }
        }
    }
}
