using System;

[Serializable]
public struct SignInWithIdpRequest {
    public string postBody;
    public string requestUri;
    public bool returnSecureToken;
}