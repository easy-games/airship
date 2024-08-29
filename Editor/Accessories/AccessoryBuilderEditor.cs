using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AccessoryBuilder))]
//[CanEditMultipleObjects]
public class AccessoryBuilderEditor : UnityEditor.Editor{

    private bool downloading = false;
    private bool canceled = false;

    public override void OnInspectorGUI() {
        AccessoryBuilder builder = (AccessoryBuilder)target;
        EditorGUILayout.LabelField("Requried Setup");
        DrawDefaultInspector();

        EditorGUILayout.Space(30);

        EditorGUILayout.LabelField("Editor Tools");

        builder.currentOutfit = (AccessoryOutfit)EditorGUILayout.ObjectField("Outfit", builder.currentOutfit, typeof(AccessoryOutfit), true);
        
        GUI.enabled = !downloading;
        if (GUILayout.Button("Equip Referenced Outfit")) {
            if(builder.currentOutfit != null){
                Debug.Log("Equipping outfit " + builder.currentOutfit.name);
                builder.EquipAccessoryOutfit(builder.currentOutfit, true);
            }
        }
        
        GUILayout.Space(10);
        GUI.enabled = true;
        builder.currentUserName = EditorGUILayout.TextField("Username", builder.currentUserName);
        builder.currentUserId = EditorGUILayout.TextField("User ID", builder.currentUserId);
        GUI.enabled = !downloading;
        if (GUILayout.Button("Equip User Outfit")) {
            if(!string.IsNullOrEmpty(builder.currentUserName)){
                DownloadUsernameOutfit(builder);
            }   
            if(!string.IsNullOrEmpty(builder.currentUserId)){
                Debug.Log("Equipping user outfit " + builder.currentUserId);
                DownloadUserOutfit(builder);
            }
        }
        GUI.enabled = true;
        GUILayout.Space(30);

        if (GUILayout.Button("Clear Outfit")) {
            if(downloading){
                builder.cancelPendingDownload = true;
                downloading = false;
            }
            Debug.Log("Clearing outfit.");
            builder.RemoveAllAccessories();
            builder.SetSkinColor(new Color(0.7169812f, 0.5064722f, 0.3754005f), true);
        }


        if(GUI.changed){
            EditorUtility.SetDirty(builder);
        }
    }

    private async Task DownloadUserOutfit(AccessoryBuilder builder){
        Debug.Log("Starting download");
        downloading = true;
        await builder.AddOutfitFromUserId(builder.currentUserId);
        Debug.Log("Done with download");
        downloading = false;
    }

    private async Task DownloadUsernameOutfit(AccessoryBuilder builder){
        Debug.Log("Starting download");
        downloading = true;
        await builder.EquipOutfitFromUsername(builder.currentUserName);
        Debug.Log("Done with download");
        downloading = false;
    }
}
