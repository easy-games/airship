using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using LeTai.TrueShadow.PluginInterfaces;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static UnityEditor.EditorGUILayout;

namespace LeTai.TrueShadow.Editor
{
[CanEditMultipleObjects]
[CustomEditor(typeof(TrueShadow))]
public class TrueShadowEditor : UnityEditor.Editor
{
    EditorProperty insetProp;
    EditorProperty sizeProp;
    EditorProperty spreadProp;
    EditorProperty useGlobalAngleProp;
    EditorProperty angleProp;
    EditorProperty distanceProp;
    EditorProperty colorProp;
    EditorProperty blendModeProp;
    EditorProperty multiplyCasterAlphaProp;
    EditorProperty ignoreCasterColorProp;
    EditorProperty colorBleedModeProp;
    EditorProperty disableFitCompensationProp;

#if LETAI_TRUESHADOW_DEBUG
    SerializedProperty alwayRenderProp;
#endif

    GUIContent procrastinateLabel;
    GUIContent editGlobalAngleLabel;

    static bool showExperimental;
    static bool showAdvanced;

    static Texture    warningIcon;
    static GUIStyle   hashWarningStyle;
    static GUIContent hashWarningLabel;

    void OnEnable()
    {
        insetProp                  = new EditorProperty(serializedObject, nameof(TrueShadow.Inset));
        sizeProp                   = new EditorProperty(serializedObject, nameof(TrueShadow.Size));
        spreadProp                 = new EditorProperty(serializedObject, nameof(TrueShadow.Spread));
        useGlobalAngleProp         = new EditorProperty(serializedObject, nameof(TrueShadow.UseGlobalAngle));
        angleProp                  = new EditorProperty(serializedObject, nameof(TrueShadow.OffsetAngle));
        distanceProp               = new EditorProperty(serializedObject, nameof(TrueShadow.OffsetDistance));
        colorProp                  = new EditorProperty(serializedObject, nameof(TrueShadow.Color));
        blendModeProp              = new EditorProperty(serializedObject, nameof(TrueShadow.BlendMode));
        multiplyCasterAlphaProp    = new EditorProperty(serializedObject, nameof(TrueShadow.UseCasterAlpha));
        ignoreCasterColorProp      = new EditorProperty(serializedObject, nameof(TrueShadow.IgnoreCasterColor));
        colorBleedModeProp         = new EditorProperty(serializedObject, nameof(TrueShadow.ColorBleedMode));
        disableFitCompensationProp = new EditorProperty(serializedObject, nameof(TrueShadow.DisableFitCompensation));

#if LETAI_TRUESHADOW_DEBUG
        alwayRenderProp = serializedObject.FindProperty(nameof(TrueShadow.alwaysRender));
#endif

        if (EditorPrefs.GetBool("LeTai_TrueShadow_" + nameof(showExperimental)))
        {
            showExperimental = EditorPrefs.GetBool("LeTai_TrueShadow_" + nameof(showExperimental), false);
            showAdvanced     = EditorPrefs.GetBool("LeTai_TrueShadow_" + nameof(showAdvanced),     false);
        }

        procrastinateLabel   = new GUIContent("Procrastinate", "A bug that is too fun to fix");
        editGlobalAngleLabel = new GUIContent("Edit...");

        if (!warningIcon)
        {
            warningIcon = typeof(EditorGUIUtility)
                         .GetProperty("warningIcon", BindingFlags.Static | BindingFlags.NonPublic)
                        ?.GetValue(null) as Texture;
        }

        hashWarningLabel = new GUIContent(warningIcon);
        hashWarningStyle = new GUIStyle(EditorGUIUtility.GetBuiltinSkin(EditorSkin.Inspector)
                                                        .FindStyle("WordWrappedLabel")) {
            richText = true
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var tss = targets.Cast<TrueShadow>().ToList();

        DrawPresetButtons(tss);

        Space();

        insetProp.Draw();
        sizeProp.Draw();
        spreadProp.Draw();

        var useGlobalAngle = tss.Any(s => s.UseGlobalAngle);
        var useLocalAngle  = tss.Any(s => !s.UseGlobalAngle);
        useGlobalAngleProp.Draw(GUILayout.ExpandWidth(!useGlobalAngle));
        if (useGlobalAngle)
        {
            var settingRect = GUILayoutUtility.GetLastRect();
            settingRect.xMin  += EditorGUIUtility.labelWidth + EditorGUIUtility.singleLineHeight;
            settingRect.width =  GUI.skin.button.CalcSize(editGlobalAngleLabel).x;
            if (GUI.Button(settingRect, editGlobalAngleLabel))
            {
                SettingsService.OpenProjectSettings("Project/True Shadow");
            }
        }

        if (useLocalAngle)
        {
            angleProp.Draw();
            if (useGlobalAngle)
                HelpBox("Some selected object(s) use global angle instead", MessageType.Info);
        }

        distanceProp.Draw();
        colorProp.Draw();
        var hasCustomMaterialProvider = tss.Any(s => s.UsingRendererMaterialProvider);
        if (hasCustomMaterialProvider)
        {
            using (new EditorGUI.DisabledScope(true))
                LabelField(blendModeProp.serializedProperty.displayName, "Custom Material");
        }

        if (tss.Any(s => !s.UsingRendererMaterialProvider))
        {
            blendModeProp.Draw();
            if (hasCustomMaterialProvider)
                HelpBox("Some selected object(s) use a custom material instead", MessageType.Info);
        }

        DrawAdvancedSettings();

#if TMP_PRESENT
        if (tss.Select(s => s.Graphic).OfType<TMPro.TextMeshProUGUI>().Any(tmp => tmp.fontSharedMaterials?.Length > 1))
        {
            if (GUILayout.Button("Copy to Sub-Meshes"))
            {
                foreach (var ts in tss)
                {
                    ts.CopyToTMPSubMeshes();
                }
            }

            HelpBox($"Sub-Mesh objects' shadows will not updates automatically to avoid large performance cost.\n\n" +
                    $"Call trueShadow.{nameof(TrueShadow.CopyToTMPSubMeshes)}() after you modify the text or shadow",
                    MessageType.Warning, true);
        }
#endif

        DrawHashWarning();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawPresetButtons(IEnumerable<TrueShadow> tss)
    {
        if (!ProjectSettings.Instance.ShowQuickPresetsButtons) return;

        using (new HorizontalScope())
        {
            var presets  = ProjectSettings.Instance.QuickPresets;
            var selected = GUILayout.Toolbar(-1, presets.Select(p => p.name).ToArray());
            if (selected != -1)
            {
                foreach (var ts in tss)
                {
                    Undo.RecordObject(ts, "Apply Quick Preset on " + ts.name);
                    presets[selected].Apply(ts);
                }

                EditorApplication.QueuePlayerLoopUpdate();
            }

            if (GUILayout.Button("...", GUILayout.ExpandWidth(false)))
            {
                SettingsService.OpenProjectSettings("Project/True Shadow");
            }
        }
    }

    void DrawAdvancedSettings()
    {
        using (var change = new EditorGUI.ChangeCheckScope())
        {
            showAdvanced = Foldout(showAdvanced, "Advanced Settings", true);
            using (new EditorGUI.IndentLevelScope())
                if (showAdvanced)
                {
                    multiplyCasterAlphaProp.Draw();
                    ignoreCasterColorProp.Draw();
                    colorBleedModeProp.Draw();
                    disableFitCompensationProp.Draw();

                    if (KnobPropertyDrawer.procrastinationMode)
                    {
                        var rot = GUI.matrix;
                        GUI.matrix                             =  Matrix4x4.identity;
                        KnobPropertyDrawer.procrastinationMode ^= Toggle("Be Productive", false);
                        GUI.matrix                             =  rot;
                    }
                    else
                    {
                        KnobPropertyDrawer.procrastinationMode |= Toggle(procrastinateLabel, false);
                    }

#if LETAI_TRUESHADOW_DEBUG
                    PropertyField(alwayRenderProp);
#endif
                }

            if (change.changed)
            {
                EditorPrefs.SetBool("LeTai_TrueShadow_" + nameof(showExperimental), showExperimental);
                EditorPrefs.SetBool("LeTai_TrueShadow_" + nameof(showAdvanced),     showAdvanced);
            }
        }
    }

    static readonly string[] KNOWN_TYPES = {
        "UnityEngine.UI.Image",
        "UnityEngine.UI.RawImage",
        "UnityEngine.UI.Text",
        "TMPro.TextMeshProUGUI",
        "TMPro.TMP_SubMeshUI",
        "Unity.VectorGraphics.SVGImage",
    };


    void DrawHashWarning()
    {
        var tss = targets.Cast<TrueShadow>().ToList();

        if (tss.Select(s => s.GetComponent<ITrueShadowCustomHashProvider>()).Any(s => s != null))
            return;

        var casterTypes = tss.Select(s => s.GetComponent<Graphic>().GetType())
                             .Where(t => !KNOWN_TYPES.Contains(t.FullName))
                             .ToList();

        if (casterTypes.Count == 0)
            return;

        hashWarningLabel.text = "Shadow may not update with changes";

        using (var _ = new VerticalScope(EditorStyles.helpBox))
        {
            GUILayout.Label(hashWarningLabel);
            GUILayout.Label($"True Shadow can't tell 2 <i>{casterTypes[0].Name}</i> apart." +
                            $" The shadow may not update when the <i>{casterTypes[0].Name}</i> changes.\n" +
                            $"To fix this, set the shadow CustomHash, or disable shadow caching for this element.",
                            hashWarningStyle);

            if (GUILayout.Button("More info on CustomHash", EditorStyles.linkLabel))
            {
                Application.OpenURL("https://leloctai.com/trueshadow/docs/articles/integration.html#make-sure-shadow-update");
            }

            if (GUILayout.Button("Disable Shadow Cache for this element", EditorStyles.linkLabel))
            {
                foreach (var ts in tss)
                {
                    Undo.AddComponent<DisableShadowCache>(ts.gameObject);
                }
            }
        }
    }
}
}
