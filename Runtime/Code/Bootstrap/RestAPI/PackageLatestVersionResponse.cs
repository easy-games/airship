[System.Serializable]
public class PackageLatestVersionResponse {
    public PackageVersionResponse version;
}

[System.Serializable]
public class PackageVersionResponse {
    public Package package;
}

[System.Serializable]
public class Package {
    public int assetVersionNumber;
    public int codeVersionNumber;
}