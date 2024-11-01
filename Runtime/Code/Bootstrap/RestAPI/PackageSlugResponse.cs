[System.Serializable]
public class PackageSlugResponse {
    public PackageDto pkg;
}

[System.Serializable]
public class PackageDto {
    public string id;
    public string slug;
    public string slugProperCase;
    public string name;
    public string description;
    public string organizationId;
    public string createdAt;
    public string lastVersionUpdate;
}