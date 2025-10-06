using Airship.DevConsole;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Networking.PlayerConnection;

public class PSOTraceManager : MonoBehaviour {
    /// <summary>
    /// A GraphicsStateCollection will be created when 'pso start' is run. It will persist until the next start.
    /// </summary>
    private GraphicsStateCollection activeGraphicsStateCollection;
    
    private void Awake() {
        if (!RunCore.IsClient()) return;
        
        RegisterCommands();
    }

    private void RegisterCommands() {
        var actionList = "Start | Stop | Upload | Help";
        DevConsole.AddCommand(Command.Create<string>(
            "pso",
            "",
            "Commands for managing active PSO trace.",
            Parameter.Create("Action", actionList),
            (action) => {
                if (!Debug.isDebugBuild) {
                    Debug.LogError("GraphicsStateCollection tracing is only available in development builds.");
                    return;
                }
                
                switch (action.ToLower()) {
                    case "help":
                        Debug.Log("This command is used to generate a GraphicsStateCollection that contains" +
                                  " the shaders used within a game. It is the preferred way to precook shaders," +
                                  " resulting in fewer hitches when seeing a shader in game for the first time." +
                                  " Use pso start to begin a profile and pso upload when you want to upload it to" +
                                  " Unity. This command only works in development builds.");
                        break;
                    case "start":
                    case "begin":
                        if (activeGraphicsStateCollection != null) Destroy(activeGraphicsStateCollection);
                        activeGraphicsStateCollection = new GraphicsStateCollection();
                        activeGraphicsStateCollection.BeginTrace();
                        Debug.Log("Starting a trace.");
                        break;
                    case "stop":
                    case "end":
                        if (activeGraphicsStateCollection == null || !activeGraphicsStateCollection.isTracing) {
                            Debug.LogWarning("There is no GraphicsStateCollection currently tracing. Use pso start.");
                            break;
                        }
                        
                        activeGraphicsStateCollection.EndTrace();
                        Debug.Log("Trace ended.");
                        break;
                    case "upload":
                        if (activeGraphicsStateCollection == null) {
                            Debug.LogWarning("There is no GraphicsStateCollection. Use pso start.");
                            break;
                        }
                        if (!PlayerConnection.instance.isConnected) {
                            Debug.LogError("You are not currently connected to a Unity Editor instance.");
                            break;
                        }
                        if (activeGraphicsStateCollection.isTracing) {
                            activeGraphicsStateCollection.EndTrace();
                        }
                        
                        // My understanding is that a unique GraphicsStateCollection per graphics API is necessary but
                        // not necessarily per platform. Although it would be necessary for different quality levels.
                        var qualityLevel = QualitySettings.GetQualityLevel();
                        var qualityName = QualitySettings.names[qualityLevel];
                        var graphicsApiName = SystemInfo.graphicsDeviceType.ToString();
                        var fileName = $"GraphicsStateCollection_{graphicsApiName}_{qualityName}";
                        if (activeGraphicsStateCollection.SendToEditor(fileName)) {
                            Debug.Log(
                                $"Saved to Assets/{fileName}. This collection is best used for Graphics API {graphicsApiName}" +
                                $" with quality level {qualityLevel}.");
                        } else {
                            Debug.LogError("Failed to send trace to editor.");
                        }

                        break;
                    default:
                        Debug.LogWarning($@"Invalid PSO action ""{action}"". Actions: {actionList}.");
                        break;
                }
            }
        ));
    }
}