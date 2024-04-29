using System;
using System.Collections.Generic;

// This structure doesn't exist, creating it to parse an array of elements
[Serializable]
public class ApiKeyPermissionDto {
    public List<ApiKeyPermissionElement> elements;
}


[Serializable]
public class ApiKeyPermissionElement {
    public ApiKeyPermissionData data;
    public string resourceType;
    public string resourceId;
}

[Serializable]
public class ApiKeyPermissionData {
    public string id;
    public string slug;
    public string slugProperCase;
    public string name;
    public string description;
    public string iconImageId;
    public string createdAt;
    public string visibility;
    public string lastVersionUpdate;
}

[Serializable]
public class OrganizationDto {
    public string id;
    public string slug;
    public string slugProperCase;
    public string name;
    public string description;
    public string iconImageId;
    public string createdAt;
}

