#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Code.Http.Internal;
using Code.Platform.Shared;
using JetBrains.Annotations;
using Unity.Plastic.Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Code.Authentication {
    [Serializable]
    public struct LiveStatsDto {
        int playerCount;
    }
    [Serializable]
    public struct OrgDto {
        public string createdAt;
        public string description;
        public string iconImageId;
        public string id;
        public string name;
        public string slug;
        public string slugProperCase;
    }
    [Serializable]
    public struct GameResponse {
        public GameDto game;
    }

    [Serializable]
    public struct GameDto {
        public string slug;
        public string slugProperCase;
        public int favorites;
        public int plays24h;
        public int uniquePlays24h;
        /** This will be null unless the game is archived */
        public string archivedAt;
        public string createdAt;
        public string description;
        public string iconImageId;
        /** PUBLIC or PRIVATE */
        public string visibility;
        public string id;
        public string name;
        [CanBeNull] public string lastVersionUpdate;
        [CanBeNull] public LiveStatsDto liveStats;
        public OrgDto organization;
        public int plays;
    }

    // [Serializable]
    // public struct MyGamesResponse {
    //     
    // }
    
    public enum EditorAuthSignInStatus {
        SIGNED_IN,
        SIGNED_OUT,
        LOADING,
    }
    
    [InitializeOnLoad]
    public static class EditorAuthManager {
        public static User localUser { get; set; }
        /// <summary>
        /// Fires on sign in / out. User might be undefined on sign in if not yet setup. Check signInStatus to confirm. 
        /// </summary>
        public static Action<User> localUserChanged;
        public static EditorAuthSignInStatus signInStatus = EditorAuthSignInStatus.LOADING;
        private static TaskCompletionSource<EditorAuthSignInStatus> signInTcs = new();
        private static DateTime lastRefreshTime;
        
        static EditorAuthManager() {
            AuthManager.authed += GetSelf;
            AuthManager.authed += async () => {
                StartAutoRefreshAuthTimer();
            };
            CheckAndRefreshAuth();
        }

        private static void CheckAndRefreshAuth() {
            if (DateTime.UtcNow - lastRefreshTime < TimeSpan.FromMinutes(25)) {
                StartAutoRefreshAuthTimer();
                return;
            }
            lastRefreshTime = DateTime.UtcNow;
            
            var authSave = AuthManager.GetSavedAccount();
            if (authSave == null) {
                signInStatus = EditorAuthSignInStatus.SIGNED_OUT;
                signInTcs.SetResult(signInStatus);
                return;
            }
            
            AuthManager.LoginWithRefreshToken(authSave.refreshToken).ContinueWith((Task<FirebaseTokenResponse> data) => {
                if (data == null) {
                    signInStatus = EditorAuthSignInStatus.SIGNED_OUT;
                    signInTcs.SetResult(signInStatus);
                    return;
                }
        
                InternalHttpManager.SetEditorAuthToken(data.Result.id_token);
                InternalHttpManager.editorUserId = data.Result.user_id;
                GetSelf();
                StartAutoRefreshAuthTimer();
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        private static async Task StartAutoRefreshAuthTimer() {
            // Check every 10 seconds in case computer fell asleep (relying on timeSinceLastAuth)
            await Task.Delay(TimeSpan.FromSeconds(10));
            CheckAndRefreshAuth();
        } 
        
        private static void GetSelf() {
            var self = InternalHttpManager.GetAsync($"{AirshipPlatformUrl.gameCoordinator}/users/self");
            self.ContinueWith((t) => {
                if (t.IsFaulted) {
                    Debug.LogError("Failed to fetch Airship profile information: " + t.Exception);
                    return;
                }
                if (t.Result.data == null) {
                    return;
                }
                
                localUser = JsonUtility.FromJson<TransferUserResponse>(t.Result.data).user;
                if (localUser == null) {
                    Debug.LogError("Failed to fetch Airship profile: user doesn't exist.");
                    return;
                }
                // If the user does not have an account send them to our website to finish creating it
                if (string.IsNullOrEmpty(localUser.uid)) {
                    var acceptsRestart = EditorUtility.DisplayDialog("Finish Creating Account",
                        "Head to our website to finish creating your Airship account", "Go", "Sign out");
                    if (acceptsRestart) {
                        Application.OpenURL("https://create.airship.gg/welcome");
                    } else {
                        // Sign out in case we are already signed in
                        EditorAuthManager.Logout();
                    }
                    return;
                }
                SessionState.SetString("Airship:EditorLocalUser", t.Result.data);

                signInStatus = EditorAuthSignInStatus.SIGNED_IN;
                localUserChanged.Invoke(localUser);
                signInTcs.SetResult(signInStatus);
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        public static async Task<Texture2D> DownloadProfilePicture() {
            if (localUser == null) {
                if (!string.IsNullOrEmpty(InternalHttpManager.editorUserId)) {
                    return GetDefaultProfilePictureFromUserId(InternalHttpManager.editorUserId);
                }
                return null;
            }
            if (string.IsNullOrEmpty(localUser.profileImageId)) {
                return GetDefaultProfilePictureFromUserId(localUser.uid);
            }
            
            var imageUrl = $"{AirshipPlatformUrl.cdn}/images/{localUser.profileImageId}";
            var texReq = UnityWebRequest.Get(imageUrl);
            texReq.downloadHandler = new DownloadHandlerTexture();
            await texReq.SendWebRequest();

            if (texReq.result != UnityWebRequest.Result.Success || texReq.downloadedBytes == 0) {
                return GetDefaultProfilePictureFromUserId(localUser.uid);
            }
            
            return ((DownloadHandlerTexture)texReq.downloadHandler).texture;
        }

        
        /// <summary>
        /// Same logic should also be at PlayerSingleton.ts
        /// </summary>
        public static Texture2D GetDefaultProfilePictureFromUserId(string userId) {
            var files = new List<string> {
                "Packages/gg.easy.airship/Editor/ProfileIcons/BlueDefaultProfilePicture.png",
                "Packages/gg.easy.airship/Editor/ProfileIcons/RedDefaultProfilePicture.png",
                "Packages/gg.easy.airship/Editor/ProfileIcons/GreenDefaultProfilePicture.png",
                "Packages/gg.easy.airship/Editor/ProfileIcons/PurpleDefaultProfilePicture.png",
            };

            var idx = userId[^1] % files.Count;
            var path = files[idx];
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        
        public static void Logout() {
            signInStatus = EditorAuthSignInStatus.SIGNED_OUT;
            localUser = null;
            AuthManager.ClearSavedAccount();
            localUserChanged.Invoke(null);
        }

        [ItemCanBeNull]
        public static async Task<List<GameDto>> FetchMyGames() {
            await signInTcs.Task;
            if (signInStatus != EditorAuthSignInStatus.SIGNED_IN) return null;
            
            var res = await InternalHttpManager.GetAsync(AirshipPlatformUrl.contentService + "/memberships/games/self?liveStats=false");
            if (!res.success) return null;
            
            var myGames = JsonConvert.DeserializeObject<List<GameDto>>(res.data);
            return myGames;
        }
        
        public static async Task<GameDto?> GetGameInfo(string gameId) {
            var res = await InternalHttpManager.GetAsync(AirshipPlatformUrl.contentService + "/games/game-id/" + gameId);
            if (!res.success) return null;
            if (res.data.Length == 0) return null; // No response = fail
            
            var gameResponse = JsonConvert.DeserializeObject<GameResponse>(res.data);
            return gameResponse.game;
        }
    }
}
#endif