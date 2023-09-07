using System;

[Serializable]
public class FirebaseTokenRequest {
    public string grant_type;
    public string refresh_token;
}