# Unity Scriptable Build Pipeline

The Scriptable Build Pipeline (SBP) package allows you to control how Unity builds content. The package moves the previously C++-only build pipeline code to a public C# package with a pre-defined build flow for building AssetBundles. The pre-defined AssetBundle build flow reduces build time, improves incremental build processing, and provides greater flexibility than before.

The [Addressables](https://docs.unity3d.com/Packages/com.unity.addressables@latest) package uses the Scriptable Build Pipeline to build AssetBundles.  For new projects it is recommended to use Addressables for your builds, rather than directly building AssetBundles using the Scriptable Build Pipeline.

If your project currently uses [BuildPipeline.BuildAssetBundle](https://docs.unity3d.com/ScriptReference/BuildPipeline.BuildAssetBundles.html) to build AssetBundles then you can update your build scripts to use the Scriptable Build Pipeline instead.  See the [Upgrade Guide](UpgradeGuide.md) for more details.

The Scriptable Build Pipeline also makes it possible to customize the build process with your own build flows, derived classes and other code that you write to run during the build itself.  However, this type of customization is intended for advanced use cases and it can present challenges when upgrading to newer versions of the Scriptable Build Pipeline.  For example, changes to the underlying package and DefaultBuildTasks may not be compatible with your customizations.  So when possible, it can be more practical to write your custom build script to perform actions before and after the AssetBundle build, rather than seeking to inject a lot of custom code directly into the build itself.
