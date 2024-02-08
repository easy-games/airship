using System;

[Serializable]
public class CreateGameDeploymentDto {
    public string gameId;
    public string minPlayerVersion;
    public string defaultScene;
}

[Serializable]
public class CreatePackageDeploymentDto {
    public string packageSlug;
}