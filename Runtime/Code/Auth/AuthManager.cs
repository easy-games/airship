using System;
using System.IO;
using JetBrains.Annotations;
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
}