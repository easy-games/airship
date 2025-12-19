using System;

[Serializable]
public struct SignInWithIdpRequest {
    public string postBody;
    public string requestUri;
    public bool returnSecureToken;
}

[Serializable]
public class FirebaseSignInWithCustomToken {
    public string token;
    public bool returnSecureToken;
}