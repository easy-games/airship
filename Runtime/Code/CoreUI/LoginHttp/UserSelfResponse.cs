using System;
using JetBrains.Annotations;

[Serializable]
public class UserSelfResponse {
    public string uid;
    [CanBeNull] public string lastUsernameChangeTime;
}