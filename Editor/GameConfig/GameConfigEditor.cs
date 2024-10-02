using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Editor.Auth;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameConfig))]
public class GameConfigEditor : UnityEditor.Editor {
    public static bool markPublishTargetPinged = false;
    private bool publishTargetPinged = false;
    
    private PublishTargetPopup gameSelectionPopup;
    private GameDto currentlySelectedGame;
    private Task<List<GameDto>> myGames;
    private Action<UpdateSelectedGame> updateSelectedGame;
    private Action requestRefresh;

    Rect buttonRect;
    public override void OnInspectorGUI() {
        serializedObject.Update();
        
        var oldBg = GUI.backgroundColor;
        var oldColor = GUI.color;
        if (publishTargetPinged) GUI.color = Color.yellow;
        GUILayout.BeginHorizontal();
        GUILayout.Label("Publish target");
        GUI.backgroundColor = oldBg;
        GUI.color = oldColor;
        var selectTitle = "<none>";
        if (currentlySelectedGame.id != null && currentlySelectedGame.id.Length > 0) {
            selectTitle = currentlySelectedGame.name;
        }
        // Debug.Log("Selected game: " + currentlySelectedGame.id);
        var selectPublishTarget = EditorGUILayout.DropdownButton(new GUIContent(selectTitle), FocusType.Keyboard, GUILayout.Width(200));
        if (Event.current.type == EventType.Repaint) buttonRect = GUILayoutUtility.GetLastRect();
        if (selectPublishTarget) {
            gameSelectionPopup = new PublishTargetPopup(serializedObject.FindProperty("gameId").stringValue, myGames, updateSelectedGame, requestRefresh);
            PopupWindow.Show(buttonRect, gameSelectionPopup);
        }
        GUILayout.EndHorizontal();

        foreach (var field in typeof(GameConfig).GetFields()) {
            if (field.Name is "gameId" or "gameLayers" || Attribute.IsDefined(field, typeof(HideInInspector))) continue; // Rendered above

            var serializedProp = serializedObject.FindProperty(field.Name);
            if (serializedProp == null) continue;
            EditorGUILayout.PropertyField(serializedProp);
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// If user is signed in this will try to fetch their first game and set it as the publish target (as long
    /// as that game has never been published to before). Ideally your first publish your flow is:
    /// Create on create.airship.gg -> click publish
    /// </summary>
    public static async Task<GameDto?> TryFetchFirstGame() {
        if (EditorAuthManager.signInStatus == EditorAuthSignInStatus.SIGNED_OUT) return null;

        var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
        if (gameConfig == null) return null;
        if (!string.IsNullOrEmpty(gameConfig.gameId)) return null; // Already have a game set
        var editorPrefsKey = "airship-ignore-first-game-fetch";
        var editorPrefsVal = "true";
        if (EditorPrefs.GetString(editorPrefsKey) == editorPrefsVal) return null; // Cached as no longer relevant
        
        var myGames = await EditorAuthManager.FetchMyGames();
        if (myGames == null) return null;
        if (myGames.Count == 0) return null;
        // Has multiple games
        if (myGames.Count > 1) {
            EditorPrefs.SetString(editorPrefsKey, editorPrefsVal);
            return null;
        }
        // Has already published to their only game (meaning this Unity instance is possibly for a new game?)
        if (myGames[0].lastVersionUpdate != null) {
            EditorPrefs.SetString(editorPrefsKey, editorPrefsVal);
            return null;
        }

        var so = new SerializedObject(gameConfig);
        so.FindProperty("gameId").stringValue = myGames[0].id;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(gameConfig);
        AssetDatabase.SaveAssetIfDirty(gameConfig);
        EditorPrefs.SetString(editorPrefsKey, editorPrefsVal);
        return myGames[0];
    }
    
    private void OnEnable() {
        if (markPublishTargetPinged) {
            markPublishTargetPinged = false;
            publishTargetPinged = true;
        }
        
        updateSelectedGame += (update) => {
            var gameId = update.gameId;
            
            serializedObject.FindProperty("gameId").stringValue = gameId;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(serializedObject.targetObject);
            AssetDatabase.SaveAssetIfDirty(serializedObject.targetObject);
            // If we didn't find a game don't close popup (display an error instead)
            if (update.gameDto.id != null) {
                gameSelectionPopup.editorWindow.Close();
            }

            publishTargetPinged = false;

            currentlySelectedGame = update.gameDto;
            Repaint();
        };

        requestRefresh += () => {
            EditorApplication.delayCall += FetchGamesAndRepaint;
        };

        EditorAuthManager.GetGameInfo(serializedObject.FindProperty("gameId").stringValue).ContinueWith((t) => {
            if (!t.Result.HasValue) return;
            currentlySelectedGame = t.Result.GetValueOrDefault();
            Repaint();
        }, TaskScheduler.FromCurrentSynchronizationContext());
        
        FetchGamesAndRepaint();
        EditorAuthManager.localUserChanged += (u) => {
            FetchGamesAndRepaint();
        };

        EditorApplication.focusChanged += OnFocusChanged;
    }

    private void FetchGamesAndRepaint() {
        myGames = EditorAuthManager.FetchMyGames();
        gameSelectionPopup?.UpdateMyGames(myGames);
        
        myGames.ContinueWith((t) => {
            Repaint();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private async void OnFocusChanged(bool focussed) {
        if (!focussed) return;

        FetchGamesAndRepaint();
        
        var game = await TryFetchFirstGame();
        if (game.HasValue) {
            currentlySelectedGame = game.Value;
            Repaint();
        }
    }

    private void OnDestroy() {
        if (gameSelectionPopup != null && gameSelectionPopup.editorWindow != null) {
            gameSelectionPopup.editorWindow.Close();
        }
        EditorApplication.focusChanged -= OnFocusChanged;
    }
    
    public static void FocusGameConfig() {
        var gameConfig = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/GameConfig.asset");
        if (gameConfig == null) {
            Debug.LogError("Missing GameConfig (at Assets/GameConfig.asset");
            return;
        }

        // Select the object
        Selection.activeObject = gameConfig;
            
        // Ping the object (this will highlight it in the Project window and update the Inspector)
        EditorGUIUtility.PingObject(gameConfig);
            
        // Ensure the Inspector window is visible and focused
        var inspectorType = Type.GetType("UnityEditor.InspectorWindow,UnityEditor.dll");
        EditorWindow.FocusWindowIfItsOpen(inspectorType);
    }
}