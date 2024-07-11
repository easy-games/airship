using System;
using UnityEngine;
using UnityEditor;

public class SelectObjWithGID : EditorWindow
{
    string myString = "";
    bool groupEnabled;
    bool myBool = true;
    float myFloat = 1.23f;

    // Add menu named "My Window" to the Window menu
    [MenuItem("Window/Select Object With Instance ID")]
    static void Init()
    {
        // Get existing open window or if none, make a new one:
        SelectObjWithGID window = (SelectObjWithGID)EditorWindow.GetWindow(typeof(SelectObjWithGID));
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Select Object With Intance ID", EditorStyles.boldLabel);
        myString = EditorGUILayout.TextField("Instance ID", myString);

        if (GUILayout.Button("Select Game Object With Instance ID"))
        {
            int myID;
            bool found = false;
            if (int.TryParse(myString, out myID)) {
                UnityEngine.Object[] all = (UnityEngine.Object[])FindObjectsOfType(typeof(UnityEngine.Object));
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i].GetInstanceID() == myID)
                    {
                        found = true;
                        if (all[i] is GameObject)
                        {
                            Selection.activeGameObject = (GameObject)all[i];
                        }
                        else if (all[i] is Component)
                        {
                            Selection.activeGameObject = ((Component)all[i]).gameObject;
                        }
                    }
                }
                if (!found)
                {
                    Debug.LogError("Could not find an object or component in the scene with the entered ID.");
                }
            } else
            {
                Debug.LogError("The number entered was not a number.");
            }
        }
    }
}
