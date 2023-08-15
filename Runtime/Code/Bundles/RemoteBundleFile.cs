public readonly struct RemoteBundleFile
{
    public string fileName { get; }
    public string Url { get; }
    public string BundleId { get; }
    public string BundleVersion { get; }

    public RemoteBundleFile(string fileName, string url, string bundleId, string bundleVersion)
    {
        this.fileName = fileName;
        this.Url = url;
        this.BundleId = bundleId;
        this.BundleVersion = bundleVersion;
    }
}