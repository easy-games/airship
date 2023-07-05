using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MTAssets.SkinnedMeshCombiner.Editor
{
    /*
     * This class is responsible for detecting if Unity Project have another SRP's.
     */

    [InitializeOnLoad]
    public class CurrentSRP
    {
        //Variable of built RP in detector 
        public static ListRequest requestListAllPackages;

        static CurrentSRP()
        {
            //Run the checker (unregister automatically after get list of packages)
            if (requestListAllPackages == null)
                requestListAllPackages = Client.List();
            // EditorApplication.update += VerifyIfHaveAnotherRenderPipelinePackage;
        }

        public static void VerifyIfHaveAnotherRenderPipelinePackage()
        {
            //If request is done
            if (requestListAllPackages.IsCompleted == true)
            {
                bool have = false;
                string packageName = "";

                //Scan all packages, and if is using BuiltIn Render Pipeline, return true
                foreach (UnityEditor.PackageManager.PackageInfo package in requestListAllPackages.Result)
                {
                    if (package.name.Contains("render-pipelines.universal"))
                    {
                        have = true;
                        packageName += (packageName.Length > 0) ? " | URP" : "URP";
                    }
                    if (package.name.Contains("render-pipelines.high-definition"))
                    {
                        have = true;
                        packageName += (packageName.Length > 0) ? " | HDRP" : "HDRP";
                    }
                    if (package.name.Contains("render-pipelines.lightweight"))
                    {
                        have = true;
                        packageName += (packageName.Length > 0) ? " | Lightweight" : "Lightweight";
                    }
                }

                //Unregister this method from Editor update
                EditorApplication.update -= VerifyIfHaveAnotherRenderPipelinePackage;

                //Apply all changes in CurrentRP script
                ApplyChanges(have, packageName);
                return;
            }
        }

        public static void ApplyChanges(bool haveAnotherRenderPipelinePackage, string renderPipelinePackageName)
        {
            //Load CurrentRP script text versions
            string currentRpTextTrue = File.ReadAllText("Assets/MT Assets/Skinned Mesh Combiner/Scripts/ProcessedByCurrentSRPInEditor/VariableTrue.txt");
            string currentRpTextFalse = File.ReadAllText("Assets/MT Assets/Skinned Mesh Combiner/Scripts/ProcessedByCurrentSRPInEditor/VariableFalse.txt");

            //Update the CurrentRP script
            if (haveAnotherRenderPipelinePackage == true)
                if (CurrentRenderPipeline.haveAnotherSrpPackages == false || CurrentRenderPipeline.packageDetected != renderPipelinePackageName)
                {
                    File.WriteAllText("Assets/MT Assets/Skinned Mesh Combiner/Scripts/ProcessedByCurrentSRPInEditor/CurrentRenderPipeline.cs", currentRpTextTrue.Replace("%package%", renderPipelinePackageName));
                    AssetDatabase.Refresh();
                }
            if (haveAnotherRenderPipelinePackage == false)
                if (CurrentRenderPipeline.haveAnotherSrpPackages == true || CurrentRenderPipeline.packageDetected != renderPipelinePackageName)
                {
                    File.WriteAllText("Assets/MT Assets/Skinned Mesh Combiner/Scripts/ProcessedByCurrentSRPInEditor/CurrentRenderPipeline.cs", currentRpTextFalse.Replace("%package%", renderPipelinePackageName));
                    AssetDatabase.Refresh();
                }
        }
    }
}