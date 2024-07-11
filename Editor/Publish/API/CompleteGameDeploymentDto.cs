using System;

[Serializable]
public class CompleteGameDeploymentDto {
    public string gameId;
    public string gameVersionId;
    public string[] uploadedFileIds;
}

[Serializable]
public class CompletePackageDeploymentDto {
    public string packageSlug;
    public string packageVersionId;
    public string[] uploadedFileIds;
}