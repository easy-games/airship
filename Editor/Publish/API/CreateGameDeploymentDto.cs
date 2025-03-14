using System;

[Serializable]
public class CreateGameDeploymentDto {
    public string gameId;
    public string minPlayerVersion;
    public string defaultScene;
    public bool deployCode;
    public bool deployAssets;
    public string[] packageSlugs;
    public string[] platforms;
}

[Serializable]
public class CreatePackageDeploymentDto {
    public string packageSlug;
    public bool deployCode;
    public bool deployAssets;
}