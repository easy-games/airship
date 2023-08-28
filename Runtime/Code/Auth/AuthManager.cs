using System;
using System.Collections.Generic;
using Code.Auth;
using Firebase;
using Firebase.Auth;
using SocketIOClient;

[LuauAPI]
public class AuthManager : Singleton<AuthManager> {
    private FirebaseAuth auth;

    public string token;

    private void Awake() {
        this.auth = FirebaseAuth.GetAuth()
    }

    public void Login() {

    }
}