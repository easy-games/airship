using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Editor.Auth;
using UnityEditor;
using UnityEngine;

public struct UpdateSelectedGame {
    public string gameId;
    public GameDto gameDto;
}

public class PublishTargetPopup : PopupWindowContent {
    private GUIStyle errorStyle;
    private Task<List<GameDto>> myGames;
    private string initialSelectedTarget;
    private string selectedTarget;
    private Vector2 scrollPosition;
    private Action<UpdateSelectedGame> updateSelectedGame;
    private Action requestRefresh;
    private bool noGameFound;
    private bool processingSubmit;
    private Dictionary<string, bool> openOrgs = new Dictionary<string, bool>();

    public PublishTargetPopup(string selectedTarget, Task<List<GameDto>> myGames, Action<UpdateSelectedGame> updateSelectedGame, Action requestRefresh) {
        this.myGames = myGames;
        this.selectedTarget = selectedTarget;
        initialSelectedTarget = selectedTarget;
        this.updateSelectedGame = updateSelectedGame;
        this.requestRefresh = requestRefresh;
        
        errorStyle = new(GUI.skin.label);
        // https://www.foundations.unity.com/fundamentals/color-palette
        errorStyle.normal.textColor = new Color(0.827f, 0.133f, 0.133f);
        errorStyle.wordWrap = true;
    }

    public void UpdateMyGames(Task<List<GameDto>> newMyGames) {
        newMyGames.ContinueWith((t) => {
            myGames = t;
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }
    
    public override Vector2 GetWindowSize()
    {
        return new Vector2(200, 300);
    }

    public override void OnGUI(Rect rect) {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        PaintMyGames();
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Target game id:");
        if (initialSelectedTarget == selectedTarget || processingSubmit) {
            GUI.enabled = false;
        }
        if (GUILayout.Button("Submit")) {
            ProcessSubmit();
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();
        selectedTarget = GUILayout.TextField(selectedTarget, GUILayout.Width(195));
        if (noGameFound) {
            GUILayout.Label("No game found with this id.", errorStyle);
        }
        
        if (myGames.IsCompleted) {
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Create game")) {
                ClickedCreateGame();
            }

            if (GUILayout.Button(new GUIContent("", EditorGUIUtility.IconContent("Refresh").image))) {
                requestRefresh.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private async void ProcessSubmit() {
        processingSubmit = true;
        var gameInfo = await EditorAuthManager.GetGameInfo(selectedTarget);
        updateSelectedGame.Invoke(new () { gameId = selectedTarget, gameDto = gameInfo.GetValueOrDefault() });
        if (!gameInfo.HasValue) {
            noGameFound = true;
        }
        initialSelectedTarget = selectedTarget;
        processingSubmit = false;
    }

    private Rect signInRect;
    private void PaintMyGames() {
        // This should only happen when C# refreshes
        if (myGames == null) {
            GUILayout.Label("Games did not load. Try to reopen Game Config.");
            return;
        }
        
        var myGamesCompleted = myGames.IsCompleted;
        if (!myGamesCompleted) {
            // GUILayout.Label("Fetching your games...");
            return;
        }
        
        var myGamesFailed = myGames.Result == null || myGames.Exception != null;
        if (myGamesFailed) {
            // Not signed in
            if (myGames.Exception == null) {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Sign in to see your games.", new GUIStyle(GUI.skin.label) { wordWrap = true });
                var signInButtonPressed = GUILayout.Button("Sign in");
                if (Event.current.type == EventType.Repaint) signInRect = GUILayoutUtility.GetLastRect();
                if (signInButtonPressed) {
                    GenericMenu menu = new GenericMenu();

                    menu.AddItem(new GUIContent("Sign in with Google"), false,
                        () => { AuthManager.AuthWithGoogle(); });

                    menu.DropDown(signInRect);
                }
                GUILayout.EndHorizontal();
                return;
            }

            var exception = "not signed in";
            if (myGames.Exception != null) {
                exception = myGames.Exception.Message;
                Debug.LogError(myGames.Exception);   
            }
            GUILayout.Label("Could not fetch games: " + exception, errorStyle);
            return;
        }

        var validGames = myGames.Result.Where((dto => dto.archivedAt == null)).ToList();
        
        if (validGames.Count == 0) {
            GUILayout.Label("You don't have any games yet.");
            return;
        }
        
        var orgs = new Dictionary<string, OrgDto>();
        var orgToGames = new Dictionary<string, List<GameDto>>();
        foreach (var g in validGames) {
            orgs.TryAdd(g.organization.id, g.organization);
            if (!orgToGames.TryGetValue(g.organization.id, out var orgGames)) {
                var games = new List<GameDto>();
                games.Add(g);
                orgToGames[g.organization.id] = games;
                continue;
            }
            orgGames.Add(g);
        }
        
        var first = true;
        foreach (var (orgId, games) in orgToGames) {
            var org = orgs[orgId];

            if (first) {
                first = false;
            } else {
                EditorGUILayout.Space();
            }

            // EditorGUILayout.Foldout(true, org.name);
            var open = openOrgs[org.id] = EditorGUILayout.BeginFoldoutHeaderGroup(openOrgs.ContainsKey(org.id) ? openOrgs[org.id] : true, org.name);
            // GUILayout.Label(org.name, EditorStyles.boldLabel);
            // GenericMenu menu = new GenericMenu();
            if (open) {
                foreach (var g in games) {
                    var toggleStyle = new GUIStyle(GUI.skin.button);
                    toggleStyle.normal.background = null;
                    toggleStyle.hover.background = null;
                    toggleStyle.active.background = null;
                    toggleStyle.wordWrap = true;
                    toggleStyle.alignment = TextAnchor.MiddleLeft;
                    // toggleStyle.active.background = new Texture2D(1, 1);
                    // toggleStyle.hover.background = new Texture2D(1, 1);
                    // toggleStyle.normal.background = new Texture2D(1, 1);
                    // toggleStyle.active.background.SetPixel(0, 0, new Color(0.7f, 0.7f, 0.7f));
                    // toggleStyle.active.background.Apply();
                    // toggleStyle.normal.background.SetPixel(0, 0, new Color(0,0,0, 1f));
                    // toggleStyle.normal.background.Apply();
                    // toggleStyle.hover.background.SetPixel(0, 0, new Color(0.8f, 0.8f, 0.8f));
                    // toggleStyle.hover.background.Apply();
                    toggleStyle.padding = new RectOffset(8, 8, 4, 4);

                    // menu.AddItem(new GUIContent(g.name), g.id == selectedTarget, () => {
                    //     selectedTarget = g.id;
                    // });

                    var lastUpdateStr = "never";
                    if (g.lastVersionUpdate != null) {
                        var lastUpdateTime = DateTime.Parse(g.lastVersionUpdate);
                        var timeSinceLastUpdate = DateTime.Now.Subtract(lastUpdateTime);
                        lastUpdateStr = ToPrettyFormat(timeSinceLastUpdate);
                    }

                    var tooltip =
                        "<b>" + g.name + "</b>\n" +
                        "@" + org.slugProperCase + "\n\n" +
                        "Visibility: " + (g.visibility.ToLower()) + "\n" +
                        "Last update: " + lastUpdateStr + "\n" +
                        "Plays: " + g.plays;
                    if (GUILayout.Button(new GUIContent(g.name, tooltip), toggleStyle)) {
                        selectedTarget = g.id;
                        updateSelectedGame.Invoke(new() { gameId = g.id, gameDto = g });
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
            // menu.ShowAsContext();
        }
    }

    private static void ClickedCreateGame() {
        #if AIRSHIP_STAGING 
        const string url = "https://create-staging.airship.gg/";
        #else
        const string url = "https://create.airship.gg/";
        #endif
        
        Application.OpenURL(url);
    }
    
    
    // https://stackoverflow.com/questions/5438363/timespan-pretty-time-format-in-c-sharp
    private static string ToPrettyFormat(TimeSpan span) {
        if (span == TimeSpan.Zero) return "0 seconds";

        var sb = new StringBuilder();
        if (span.Days > 0)
            sb.AppendFormat("{0} day{1} ", span.Days, span.Days != 1 ? "s" : String.Empty);
        else if (span.Hours > 0)
            sb.AppendFormat("{0} hour{1} ", span.Hours, span.Hours != 1 ? "s" : String.Empty);
        else if (span.Minutes > 0)
            sb.AppendFormat("{0} minute{1} ", span.Minutes, span.Minutes != 1 ? "s" : String.Empty);
        else if (span.Seconds > 0)
            sb.AppendFormat("{0} second{1} ", span.Seconds, span.Seconds != 1 ? "s" : String.Empty);
        sb.Append("ago");
        return sb.ToString();

    }

    public override void OnOpen() {
        if (myGames == null) return;
        
        myGames.ContinueWith((t) => {
            editorWindow.Repaint();
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    public override void OnClose() {
        
    }
}
