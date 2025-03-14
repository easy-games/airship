using System;
// ReSharper disable InconsistentNaming

[Serializable]
public class DeploymentDto {
    public DeploymentUrls urls;
    public DeploymentVersionDto version;
}

[Serializable]
public class DeploymentVersionDto {
    public string gameVersionId;
    public string gameId;
    public int assetVersionNumber;
    public int codeVersionNumber;
    public string packageVersionId;
}

[Serializable]
public class DeploymentUrls {
    public string Linux_client_resources;
    public string Linux_shared_resources;
    public string Linux_server_resources;
    public string Linux_client_scenes;
    public string Linux_shared_scenes;
    public string Linux_server_scenes;

    public string Mac_client_resources;
    public string Mac_shared_resources;
    public string Mac_client_scenes;
    public string Mac_shared_scenes;

    public string Windows_client_resources;
    public string Windows_shared_resources;
    public string Windows_client_scenes;
    public string Windows_shared_scenes;

    public string iOS_client_resources;
    public string iOS_shared_resources;
    public string iOS_client_scenes;
    public string iOS_shared_scenes;

    public string Android_shared_resources;
    public string Android_shared_scenes;

    public string gameConfig;
    public string source;
    public string code;
}