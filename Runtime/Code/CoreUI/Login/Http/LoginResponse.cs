using System;

[Serializable]
public class LoginResponse {
    public string idToken;
    public string refreshToken;
}

[Serializable]
public class SteamInGameLoginResponse {
    public string firebaseToken;
}