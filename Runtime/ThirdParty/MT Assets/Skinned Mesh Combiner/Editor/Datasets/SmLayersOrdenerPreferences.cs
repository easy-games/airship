using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTAssets.SkinnedMeshCombiner.Editor
{
    /*
     * This script is the Dataset of the scriptable object "Preferences". This script saves Sprite Meshes Layers Ordener preferences.
     */

    public class SmLayersOrdenerPreferences : ScriptableObject
    {
        public string projectName;
        public Rect windowPosition;
    }
}