using System;
using System.IO;
using Cdm.Authentication.Clients;
using JetBrains.Annotations;
using Proyecto26;
using RSG;
using UnityEngine;

[LuauAPI(LuauContext.Protected)]
public class AuthManager {

   private static string GetAccountJSONPath() {
#if UNITY_EDITOR
      return Path.Combine(Application.persistentDataPath, "account_editor.json");
#endif
      return Path.Combine(Application.persistentDataPath, "account.json");
   }

   [CanBeNull]
   public static AuthSave GetSavedAccount() {
      var path = GetAccountJSONPath();
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
      var path = GetAccountJSONPath();
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
      var path = GetAccountJSONPath();
      if (File.Exists(path)) {
         File.Delete(path);
      }
   }
}