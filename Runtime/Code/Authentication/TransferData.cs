using System;

[Serializable]
public class User {
    public string uid;
    public string username;
    public string profileImageId;
}


[Serializable]
public class TransferData {
    public User user;
}