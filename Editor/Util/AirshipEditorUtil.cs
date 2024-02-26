using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

public class AirshipEditorUtil {
    public static bool AllRequestsDone(List<UnityWebRequestAsyncOperation> requests)
    {
        // A little Linq magic
        // returns true if All requests are done
        return requests.All(r => r.isDone);
    }

    private static System.Type m_ConsoleWindowType = null;
    private static EditorWindow GetConsoleWindow()
    {
        if (m_ConsoleWindowType == null)
        {
            var editorWindowTypes = TypeCache.GetTypesDerivedFrom<EditorWindow>();
            foreach (var t in editorWindowTypes)
            {
                if (t.Name == "ConsoleWindow")
                {
                    m_ConsoleWindowType = t;
                    break;
                }
            }
            if (m_ConsoleWindowType == null)
                throw new System.Exception("Error could not find ConsoleWindow type");
        }
        return EditorWindow.GetWindow(m_ConsoleWindowType);
    }

    public static void EnsureDirectory(string path) {
        if (!Directory.Exists(path)) {
            Directory.CreateDirectory(path);
        }
    }

    public static void FocusConsoleWindow()
    {
        var consoleWindow = GetConsoleWindow();
        consoleWindow.Focus();
    }
}