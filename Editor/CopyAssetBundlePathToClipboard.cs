using UnityEngine;
using UnityEditor;

namespace EasyEditorUtilities
{

    public class ContextMenu
    {
        [MenuItem("Assets/Copy Easy Asset File Path", false, 111)]
        private static void DoSomething()
        {
            if (Selection.objects.Length == 0)
            {
                return;
            }

            UnityEngine.Object selectedObject = Selection.objects[0];
            
            string path = GetEasyAssetPath(selectedObject);
            GUIUtility.systemCopyBuffer = path;
        }
        
        public static string GetEasyAssetPath(UnityEngine.Object obj) 
        {
            string path = AssetDatabase.GetAssetPath(obj);
            Debug.Log("path: " + path);
            
        
            //manipulate the path
            string rootPath = "Assets/Game/BedWars/Bundles/";

            if (path.StartsWith(rootPath))
            {
                path = path.Substring(rootPath.Length);
            }

            string playerPath = "Assets/"; //For player etc

            if (path.StartsWith(playerPath))
            {
                path = path.Substring(playerPath.Length);
            }

            return path;
        }
    }
}