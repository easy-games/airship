using System;

[Serializable]
public class LoginResponse {
    public string idToken;
    public string refreshToken;
    /// <summary>
    /// User id
    /// </summary>
    public string localId;
}

[Serializable]
public class SteamInGameLoginResponse {
    public string firebaseToken;
}