using System;
using UnityEngine.Serialization;

[Serializable]
public class RequiredPackageDto {
    public string packageSlug;
    public string packetVersionId;
    public int assetVersionNumber;
    public int codeVersionNumber;
    public int publishVersionNumber;
}

[Serializable]
public class RequiredPackagesDto {
    public RequiredPackageDto[] packages;
}