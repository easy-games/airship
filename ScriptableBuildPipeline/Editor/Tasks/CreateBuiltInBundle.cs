using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline.Injector;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Build.Utilities;
using UnityEngine;

namespace UnityEditor.Build.Pipeline.Tasks
{
    /// <summary>
    /// Optional build task that extracts Unity's built in extras and assigns them to the specified bundle
    /// </summary>
    public class CreateBuiltInBundle : IBuildTask
    {
        static readonly GUID k_BuiltInGuid = new GUID(CommonStrings.UnityBuiltInExtraGuid);
        /// <inheritdoc />
        public int Version { get { return 1; } }

#pragma warning disable 649
        [InjectContext(ContextUsage.In)]
        IDependencyData m_DependencyData;

        [InjectContext(ContextUsage.InOut, true)]
        IBundleExplictObjectLayout m_Layout;
#pragma warning restore 649

        /// <summary>
        /// Stores the name for the built-in bundle.
        /// </summary>
        public string BuiltInBundleName {get; set; }

        /// <summary>
        /// Create the built-in bundle.
        /// </summary>
        /// <param name="bundleName">The name of the bundle.</param>
        public CreateBuiltInBundle(string builtinBundleName)
        {
            BuiltInBundleName = builtinBundleName;
        }

        /// <inheritdoc />
        public ReturnCode Run()
        {
            HashSet<ObjectIdentifier> buildInObjects = new HashSet<ObjectIdentifier>();
            foreach (AssetLoadInfo dependencyInfo in m_DependencyData.AssetInfo.Values)
            {
                foreach (var referencedObject in dependencyInfo.referencedObjects)
                {
                    if (referencedObject.guid == k_BuiltInGuid)
                    {
                        buildInObjects.Add(referencedObject);
                    }
                }
            }

            foreach (SceneDependencyInfo dependencyInfo in m_DependencyData.SceneInfo.Values)
            {
                foreach (var referencedObject in dependencyInfo.referencedObjects)
                {
                    if (referencedObject.guid == k_BuiltInGuid)
                    {
                        buildInObjects.Add(referencedObject);
                    }
                }
            }

            ObjectIdentifier[] usedSet = buildInObjects.ToArray();
            Type[] usedTypes = BuildCacheUtility.GetMainTypeForObjects(usedSet);

            if (m_Layout == null)
                m_Layout = new BundleExplictObjectLayout();

            for (int i = 0; i < usedTypes.Length; i++)
            {
                m_Layout.ExplicitObjectLocation.Add(usedSet[i], BuiltInBundleName);
            }

            if (m_Layout.ExplicitObjectLocation.Count == 0)
                m_Layout = null;

            return ReturnCode.Success;
        }
    }
}
