using System;

[Serializable]
public class RequiredPackageDto {
    public string packageSlug;
    public int versionNumber;
}

[Serializable]
public class RequiredPackagesDto {
    public RequiredPackageDto[] packages;
}