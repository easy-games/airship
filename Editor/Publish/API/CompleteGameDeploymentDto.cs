using System;

[Serializable]
public class CompleteGameDeploymentDto {
    public string gameId;
    public string gameVersionId;
}

[Serializable]
public class CompletePackageDeploymentDto {
    public string packageSlug;
    public string packageVersionId;
}