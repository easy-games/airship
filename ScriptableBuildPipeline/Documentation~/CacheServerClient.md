# About the Cache Server Client

Use the Cache Server Client to upload and download files to any Unity Cache Server. The Cache Server Client is used to communicate with a Unity Cache Server to store and retrieve incremental artifacts of the SBP build process, so that contents of the SBP build cache can be reused by multiple machines that are using the same project.

*Warning:* The [Unity Cache Server](https://docs.unity3d.com/Manual/CacheServer.html) has some performance limitations when dealing with a high volume of small cache entries. That can occur when performing large builds. The Cache Server is no longer under active development, as the Asset Import Pipeline now uses the [Unity Accelerator](https://docs.unity3d.com/Manual/UnityAccelerator.html). The Scriptable Build Pipeline retains the support to cache build artifacts through the Cache Server, as documented here, but this is not a recommended configuration.

The Unity Accelerator can speed up the Asset Import process when the same project is opened on different machines. But it does not support sharing artifacts stored in the local SBP build cache.

# Installation

To install this package, follow the instructions in the [Package Manager documentation](https://docs.unity3d.com/Packages/com.unity.package-manager-ui@latest/index.html). 

# Usage
## API Examples

This following example shows how to share build artifacts between team members or multiple machines to achieve faster build times.

Requirements:
1. A Cache Server instance dedicated to build artifacts. In addition you may run an Accelerator to speed up Asset Imports. 
2. High Reliability mode turned off on the Build Cache Server instance. The build cache uses dynamic dependencies which is incompatible with high reliability mode.
3. The build code must use the `ContentPipeline.BuildAssetBundles` method.
4. `BundleBuildParameters.UseCache` is set to true.
5. `BundleBuildParameters.CacheServerHost` and `BundleBuildParameters.CacheServerPort` are set to the cache server instance host or IP address and port respectively.

Example code:

```csharp
public static class BuildAssetBundlesExample
{
    public static bool BuildAssetBundles(string outputPath, bool useChunkBasedCompression, BuildTarget buildTarget, BuildTargetGroup buildGroup)
    {
        var buildContent = new BundleBuildContent(ContentBuildInterface.GenerateAssetBundleBuilds());
        var buildParams = new BundleBuildParameters(buildTarget, buildGroup, outputPath);
        // Set build parameters for connecting to the Cache Server
        buildParams.UseCache = true;
        buildParams.CacheServerHost = "buildcache.unitygames.com";
        buildParams.CacheServerPort = 8126;

        if (useChunkBasedCompression)
            buildParams.BundleCompression = BuildCompression.DefaultLZ4;

        IBundleBuildResults results;
        ReturnCode exitCode = ContentPipeline.BuildAssetBundles(buildParams, buildContent, out results);
        return exitCode == ReturnCode.Success;
    }
}
```


### Upload a file
```csharp
const string guidStr = "f7950ee725f9d47c7b90b02224b4534f";
const string  hashStr = "5082668810f105d565e2da3f8bf394ee";
var fileId = FileId.From(guidStr, hashStr);

var client = new Client("localhost", 8126);
client.Connect();

using(var stream = new FileStream())
{
    client.BeginTransaction(fileId);
    client.Upload(FileType.Asset, stream);
    client.EndTransaction();
}

client.Close();
```
### Download a file
```csharp
const string guidStr = "f7950ee725f9d47c7b90b02224b4534f";
const string  hashStr = "5082668810f105d565e2da3f8bf394ee";
var fileId = FileId.From(guidStr, hashStr);
var filePath = "/target/filename";

var client = new Client("localhost", 8126);
client.Connect();

// FileDownloadItem implements IDownloadItem
var downloadItem = new FileDownloadItem(fileId, FileType.Asset, filePath);
client.QueueDownload(downloadItem);

client.DownloadFinished += (object sender, DownloadFinishedEventArgs args) =>
{
    DownloadResult result = args.Result;
    long size = args.Size;
    long queueLength = args.DownloadQueueLength;
};

client.ResetDownloadFinishedEventHandler(); // cleanup
client.Close();
```
## Advanced

### IDownloadItem

Implement `IDownloadItem` to download vai WriteStream to a custom location.
## Utilities
### Upload All Assets
Quickly seed a local or remote cache server with the current project's imported assets.

1) From the Unity Editor toolbar, select `Assets -> Cache Server -> Upload All Assets`
2) Input the destination Cache Server. The currently configured global Unity Editor setting will be used by default.
3) Press Upload - for large projects, a progress dialog will display during the upload.

Or frome the Command Line:

`Unity -projectPath [projectPath] -ExecuteMethod Unity.CacheServer.CacheServerUploader.UploadAllFilesToCacheServer -batchmode -quit`

# Technical details
## Requirements

This version of the Cache Server Client is compatible with the following versions of the Unity Editor:

* 2017.1 and later (recommended)
* 5.6 and earlier may work but are untested

This Cache Server Client is compatible with the following versions of the Unity Cache Server:
* [v5.x](https://github.com/Unity-Technologies/unity-cache-server) and later (recommended)
* Other Cache Server versions shipped with Unity 5.x and later
