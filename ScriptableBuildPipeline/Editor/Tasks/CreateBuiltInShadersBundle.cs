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
    /// Optional build task that extracts Unity's built in shaders and assigns them to the specified bundle
    /// </summary>
    [Obsolete("CreateBuiltInShaders has been replaced with CreateBuiltInBundle.")]
    public class CreateBuiltInShadersBundle : IBuildTask
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
        /// Stores the name for the built-in shaders bundle.
        /// </summary>
        public string ShaderBundleName { get; set; }

        /// <summary>
        /// Create the built-in shaders bundle.
        /// </summary>
        /// <param name="bundleName">The name of the bundle.</param>
        public CreateBuiltInShadersBundle(string bundleName)
        {
            ShaderBundleName = bundleName;
        }

        /// <inheritdoc />
        public ReturnCode Run()
        {
            IBuildContext context = new BuildContext(m_DependencyData, m_Layout);
            CreateBuiltInBundle createBuiltInBundle = new CreateBuiltInBundle(ShaderBundleName);
            ContextInjector.Inject(context, createBuiltInBundle );
            ReturnCode result = createBuiltInBundle.Run();
            ContextInjector.Extract(context, createBuiltInBundle);

            m_DependencyData = context.GetContextObject<IDependencyData>();
            m_Layout = context.GetContextObject<IBundleExplictObjectLayout>();

            return result;
        }
    }
}
