using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

namespace LeTai.TrueShadow.Editor
{
[InitializeOnLoad]
public static class AutoCustomScriptingDefine
{
    internal static readonly HashSet<string> SYMBOLS = new HashSet<string> { "LETAI_TRUESHADOW" };

    static AutoCustomScriptingDefine()
    {
        Apply();
    }

    public static void Apply()
    {
        AddMissingSymbols(EditorUserBuildSettings.activeBuildTarget);
    }

    static void AddMissingSymbols(BuildTarget buildTarget)
    {
        var    currentGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
        string definesStr;
#if UNITY_2021_2_OR_NEWER
        var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(currentGroup);
        definesStr = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
#else
        definesStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(currentGroup);
#endif
        var defines = definesStr.Split(';').ToList();


        var missing = SYMBOLS.Except(defines).ToList();
        defines.AddRange(missing);

        if (missing.Count > 0)
        {
            var newDefinesStr = string.Join(";", defines);
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, newDefinesStr);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(currentGroup, newDefinesStr);
#endif
        }
    }
}
}
