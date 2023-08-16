using System.Collections.Generic;
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
}