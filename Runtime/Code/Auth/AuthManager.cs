using System;
using System.IO;
using Cdm.Authentication.Clients;
using JetBrains.Annotations;
using Proyecto26;
using RSG;
using UnityEngine;

[LuauAPI]
public class AuthManager {

   [CanBeNull]
   public static AuthSave GetSavedAccount() {
      var path = Path.Combine(Application.persistentDataPath, "account.json");
      if (!File.Exists(path)) {
         return null;
      }

      try {
         var authSave = JsonUtility.FromJson<AuthSave>(File.ReadAllText(path));
         return authSave;
      } catch (Exception e) {
         Debug.LogError(e);
      }
      return null;
   }

   public static void SaveAuthAccount(string refreshToken) {
      var authSave = new AuthSave {
         refreshToken = refreshToken,
         time = DateTimeOffset.Now.ToUnixTimeSeconds()
      };
      var path = Path.Combine(Application.persistentDataPath, "account.json");
      File.WriteAllText(path, JsonUtility.ToJson(authSave));
   }

   public static IPromise<FirebaseTokenResponse> LoginWithRefreshToken(string apiKey, string refreshToken) {
      return RestClient.Post<FirebaseTokenResponse>(new RequestHelper {
         Uri = "https://securetoken.googleapis.com/v1/token?key=" + apiKey,
         Body = new FirebaseTokenRequest {
            refresh_token = refreshToken,
            grant_type = "refresh_token"
         }
      });
   }

   public static void ClearSavedAccount() {
      var path = Path.Combine(Application.persistentDataPath, "account.json");
      if (File.Exists(path)) {
         File.Delete(path);
      }
   }
}