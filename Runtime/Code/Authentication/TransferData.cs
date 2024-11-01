using System;

[Serializable]
public class TransferUserResponse {
    public User user;
}

[Serializable]
public class User {
    public string uid;
    public string username;
    public string usernameLower;
    public string lastUsernameChangeTime;
    public string profileImageId;
}


[Serializable]
public class TransferData {
    public User user;
}