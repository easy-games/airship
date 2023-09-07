using System;

[Serializable]
public class User {
    public string uid;
    public string username;
    public string discriminator;
    public string discriminatedUsername;
}


[Serializable]
public class TransferData {
    public User user;
}