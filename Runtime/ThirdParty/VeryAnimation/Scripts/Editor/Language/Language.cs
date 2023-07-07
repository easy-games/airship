using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VeryAnimation
{
    public class Language
    {
        public enum LanguageType
        {
            English,
            Japanese,
            Total,
        }
        public static readonly string[] LanguageTypeString =
        {
            LanguageType.English.ToString(),
            LanguageType.Japanese.ToString(),
        };
        private static LanguageType languageType;

        public static void SetLanguage(LanguageType type)
        {
            languageType = type;
            if (OnLanguageChanged != null)
                OnLanguageChanged.Invoke();
        }

        public enum Help
        {
            #region MainGUI
            AnimationWindowisnotopen,
            AnimationWindowisnotfocus,
            TheSequenceEditortowhichAnimationislinkedisnotenabled,
            Noanimatableobjectselected,
            ThereisnoAnimationClip,
            GameObjectisnotActive,
            Editingonoptimizedtransformhierarchyisnotsupported,
            EditingLegacywhileplayingisnotsupported,
            EditingNoAnimatorControllernotsupported,
            AnimatorControllerisnoteditable,
            TheAnimationTracktowhichAnimationislinkedisnotenabled,
            EditingTimelinewhileplayingisnotsupported,
            TimelineGameObjectisnotActive,
            TimelinePlayableDirectorisnotEnable,
            TimelineWindowisnotfocus,
            PrefabModeEnableAutoSave,
            PrefabModeObjectNotMatch,
            TranslationDoFisdisabled,
            NotSupportUnityMessage,
            //Animation
            HelpAnimation,
            AnimationclipisReadOnly,
            AnimationClipSettingsLoopPoseisenabled,
            AnimationClipSettingsRootTransformPositionYisFeet,
            AnimationClipSettingsBasedUponisnotOriginal,
            AnimationClipSettingsBakeIntoPoseDisableRootMotion,
            AnimationClipSettingsCycleOffsetisnot0,
            AnimationClipSettingsMirrorisenabled,
            AnimationClipSettingsFramerateofTimelineandAnimationClipdonotmatch,
            AnimationTrackSettingRemoveStartOffsetsEnable,
            //Tools
            HelpTools,
            HelpToolsCopy,
            HelpToolsTrim,
            HelpToolsAdd,
            HelpToolsCombine,
            HelpToolsCreateNewClip,
            HelpToolsCreateNewKeyframe,
            HelpToolsBakeIK,
            HelpToolsAnimationRigging,
            HelpToolsHumanoidIK,
            HelpToolsRootMotion,
            HelpToolsParameterRelatedCurves,
            HelpToolsRotationCurveInterpolation,
            HelpToolsKeyframeReduction,
            HelpToolsEnsureQuaternionContinuity,
            HelpToolsCleanup,
            HelpToolsFixErrors,
            HelpToolsAnimCompression,
            HelpToolsExport,
            HelpToolsHumanoidWarning,
            HelpToolsNotAnimatorWarning,
            HelpToolsGenericAndRootNodeWarning,
            HelpToolsRotationCurveInterpolationEulerAnglesWarning,
            ToolsCopyWriteFrame,
            ToolsCreateNewKeyframeIntervalFrame,
            ToolsKeyframeReductionRootAndIKGoalCurves,
            ToolsBakeIKSimple,
            ToolsBakeIKInterpolation,
            ToolsAnimCompressionCompressed,
            ToolsAnimCompressionUseHighQualityCurve,
            //Settings
            HelpSettings,
            SettingsSaveSettings,
            SettingsPropertyStyle_Default,
            SettingsPropertyStyle_Filter,
            SettingsAutorunFrameAll,
            SettingsMirrorScale,
            SettingsSearchByName,
            SettingsIgnoreUpToTheSpecifiedCharacter,
            SettingsSynchronizeAnimation,
            SettingsOnionSkin,
            SettingsRootTrail,
            SettingsRestartOnly,
            //Help
            HelpHelp,
            HelpShortcuts,
            //Preview
            HelpPreview,
            #endregion
            #region ControlGUI
            //AnimatorIK
            HelpAnimatorIK,
            AnimatorIKChangeAll,
            AnimatorIKSelectAll,
            AnimatorIKScaleChangedErrorWarning,
            AnimatorIKAnimationRiggingEnable,
            AnimatorIKAnimationRiggingDisable,
            //OriginalIK
            HelpOriginalIK,
            OriginalIKChangeAll,
            OriginalIKSelectAll,
            OriginalIKTypeCcdIK,
            OriginalIKTypeLimbIK,
            OriginalIKTypeLockAt,
            OriginalIKResetRotations,
            OriginalIKDirection,
            OriginalIKPleasespecifyGameObject,
            OriginalIKTemplate,
            OriginalIKSaveAs,
            //Humanoid
            HelpHumanoid,
            //SelectionSet
            HelpSelectionSet,
            SelectionSetTemplate,
            SelectionSetSaveAs,
            MoveSelect,
            //Hierarchy
            HelpHierarchy,
            HierarchyHumanoidName,
            HierarchyMirrorObject,
            HierarchyMirrorAutomap,
            HierarchyMirrorClear,
            HierarchyToolbarAll,
            HierarchyToolbarWeight,
            HierarchyToolbarBody,
            HierarchyToolbarFace,
            HierarchyToolbarLeftHand,
            HierarchyToolbarRightHand,
            HierarchyToolbarRenderer,
            HierarchyToolbarRendererParent,
            #endregion
            #region EditorGUI
            //Common
            EditorMenuOnScene,
            //Options
            EditorOptionsClamp,
            EditorOptionsFootIK,
            EditorOptionsFootIK_2018_3,
            EditorOptionsMirror,
            EditorOptionsCollision,
            //RootCorrection
            EditorRootCorrection,
            EditorRootCorrectionDisable,
            EditorRootCorrectionSingle,
            EditorRootCorrectionFull,
            //Pose
            HelpPose,
            EditorPoseHumanoidReset,
            EditorPoseAvatarConfiguration,
            EditorPoseStart,
            EditorPoseTPose,
            EditorPoseBind,
            EditorPosePrefab,
            EditorPoseMirror,
            EditorPoseTemplate,
            EditorPoseSaveAs,
            EditorPoseModeAll,
            EditorPoseModeSelection,
            //BlendPose
            HelpBlendPose,
            BlendPoseNotPoseReady,
            BlendPoseModeTree,
            BlendPoseModeSelection,
            //MuscleGroup
            HelpMuscleGroup,
            //HandPose
            HelpHandPose,
            HandPoseTemplate,
            HandPoseSaveAs,
            //BlendShape
            HelpBlendShape,
            BlendShapeMirrorName,
            BlendShapeMirrorAutomap,
            BlendShapeMirrorClear,
            BlendShapeTemplate,
            BlendShapeSaveAs,
            //Selection
            HelpSelection,
            SelectionHip,
            SelectionHumanoidConflict,
            SelectionNothingisselected,
            SelectionMirror,
            SelectionUpdateIK,
            SelectionSyncIK,
            SelectionResetIK,
            SelectionAnimatorIKSpaceTypeGlobal,
            SelectionAnimatorIKSpaceTypeLocal,
            SelectionAnimatorIKSpaceTypeParent,
            SelectionOriginalIKSpaceTypeGlobal,
            SelectionOriginalIKSpaceTypeLocal,
            SelectionOriginalIKSpaceTypeParent,
            #endregion
            #region ToolsGUI
            ToolsWindowVAEditing,
            ToolsWindowHelpResetPose,
            ToolsWindowHelpTemplatePose,
            ToolsWindowHelpRemoveSaveSettings,
            ToolsWindowHelpReplaceReference,
            ToolsWindowHelpAnimationRigging,
            #endregion
            #region Dialog
            DisplayDialogSaveInsideAssetsFolder,
            DisplayDialogAnimationRemoveSaveSettings,
            DisplayDialogAnimationReplaceReference,
            #endregion
            #region Log
            LogUpdateGenericRootMotionRawEulerError,
            LogAnimationClipReadOnlyError,
            LogGenericCurveHumanoidConflictError,
            LogGenericCurveRootConflictError,
            LogParameterRelatedCurveNameChanged,
            LogFixOverRotationCurve,
            LogFixErrors,
            LogSampleBindPoseBoneLengthError,
            LogSampleBindPoseBoneNullError,
            LogSampleBindPoseUnknownError,
            LogSampleTPoseUnknownError,
            LogFailedLoadPoseError,
            LogMultipleGameObjectsWithSameName,
            LogMultipleBlendShapesWithSameName,
            LogAvatarHumanoidSetBoneError,
            LogAnimationClipReferenceReplaceError,
            LogAnimationClipAddError,
            LogAnimationWindowRecordingStartError,
            #endregion
        }
        private static readonly GUIContent[][] LanguageHelps = new GUIContent[(int)LanguageType.Total][]
        {
            #region English
            new GUIContent[]
            {
                #region MainGUI
                new GUIContent("Animation Window is not open."),
                new GUIContent("Animation Window is not focus."),
                new GUIContent("The Sequence Editor to which Animation is linked is not enabled.\nPlease press the Unlink button on Animation Window."),
                new GUIContent("No animatable object selected.\n(Animator or Animation Component)"),
                new GUIContent("No Animation Clip is specified for Component."),
                new GUIContent("GameObject is not Active."),
                new GUIContent("Editing on optimized transform hierarchy is not supported.\nPlease deoptimize transform hierarchy."),
                new GUIContent("Editing Legacy while playing is not supported."),
                new GUIContent("Editing in playing when AnimatorController is not present is not supported."),
                new GUIContent("Animator Controller is not editable."),
                new GUIContent("The Animation Track to which Animation is linked is not editable.\nPlease press the Unlink button on Animation Window."),
                new GUIContent("Editing Timeline while playing is not supported."),
                new GUIContent("Timeline GameObject is not Active."),
                new GUIContent("Timeline Playable Director is not Enable."),
                new GUIContent("Timeline Window is not focus."),
                new GUIContent("To edit in Prefab Mode you need to disable Auto Save in Prefab Mode."),
                new GUIContent("The objects selected in Prefab Mode and Animation Window do not match.\nPlease unlock the Animation Window and select it again."),
                new GUIContent("{0} is not found.\n{1} Translation DoF is disabled."),
                new GUIContent("Operation with this version of Unity is unsupported.\nTherefore, it may not operate normally."),
                //Animation
                new GUIContent("This is animation information being edited."),
                new GUIContent("Animation clip is Read-Only.\nPlease select editable animation clip in Animation window.\nAlternatively, duplicate the selected animation clip from the button."),
                new GUIContent("Loop Pose (Blend) of Animation Clip setting is enabled.\nIn this state, the closer to the end of the animation, the larger the difference due to the correction when playing back."),
                new GUIContent("Animation Clip Settings Root Transform Position (Y) is Feet.\nHowever, Foot IK Keyframe does not exist.\nIt must be created."),
                new GUIContent("Animation Clip's Based Upon is not Original or Offset is not 0.\nIn this state, the processing is added during playback, and the position is shifted from the editing Skeleton.\nIf this is not intentional, change it to Original and 0."),
                new GUIContent("Animation Clip Settings Bake Into Pose is disabled and the Animator 's Apply Root Motion is also disabled.\nWith this combination, it will behave unintentionally when playing back.\nPlease change either after you finish editing."),
                new GUIContent("Animation Clip Settings Cycle Offset is not 0.\nIn this case Animator IK will not result in normal results.\nPlease set 0."),
                new GUIContent("Animation Clip Settings Mirror is enabled.\nSince it does not correspond, it does not operate normally.\nPlease disable it."),
                new GUIContent("The frame rate of Timeline and Animation Clip do not match.\nBecause it causes problems, please set same.\n Animation Clip {0} != Timeline {1}"),
                new GUIContent("Remove Start Offset of Animation Playable Asset setting is enabled.\nIn this state, the processing is added during playback, and the position is shifted from the editing Skeleton.\nIf this is not what you intended, change it to Disabled."),
                //Tools
                new GUIContent("This is a variety of animation related processing."),
                new GUIContent("Copy a range of keyframes and events."),
                new GUIContent("Trim the animation clip."),
                new GUIContent("Append the contents of the specified Clip after the current Animation Clip."),
                new GUIContent("Combines the specified Clip to the current Animation Clip.\nDuplicate Animation Curve will be overwritten."),
                new GUIContent("Create a new animation clip.\nThe settings of AnimationClip and AnimatorState are copied."),
                new GUIContent("Creates a keyframe at the specified interval to the selected bone."),
                new GUIContent("Executes IK in the specified range.\nThe 'Clamp', 'Foot IK' and 'Mirror' settings in Options will affect the results."),
                new GUIContent("Edit the Animation Rigging curve.\nIn order to generate, Constraint must be set.\nTo use it, you need to have Animation Rigging Package installed on Unity 2019.2 or later."),
                new GUIContent("Edit animation curve used when Foot IK of Animator State is valid.\nIt is used only for Humanoid Motion."),
                new GUIContent("Edit Root Motion information.\nThis function is available for Humanoid and Generic types."),
                new GUIContent("Edit animation curve that relays the value to Animator Controller Parameter.\nThe only valid parameter type is 'Float'.\nIt is used only for Humanoid and Generic Motion."),
                new GUIContent("Change the interpolation mode of all Transform Rotation Curves.\nIt is recommended to standardize to Quaternion."),
                new GUIContent("Perform keyframe reduction with the specified error value.\nFor the setting items, refer to Anim.Compression of ModelImporter.\nWhen executed, the interpolation mode of Transform Rotation Curves is changed to Quaternion."),
                new GUIContent("Execute 'AnimationClip.EnsureQuaternionContinuity'.\nThe quaternion curve is modified."),
                new GUIContent("Removes the selected contents from the animation clip."),
                new GUIContent("It fix the condition where error or assert occurs.\nNormally you do not need to run it."),
                new GUIContent("Change the animation compression settings to reduce the build size.\nThe default settings when creating anim are Compressed disabled and High Quality Curve enabled.\nCombinations that cannot be set with Model Importer cannot be used."),
                new GUIContent("Export the Collada files.\nCollada files can be converted to FBX files with other tools."),
                new GUIContent("Only Animation Type Humanoid will be effective."),
                new GUIContent("Only Animation Type Humanoid or Generic will be effective."),
                new GUIContent("This is valid only when the Generic type and Root node are specified."),
                new GUIContent("This conversion may cause problems such as reverse rotation."),
                new GUIContent("Write", "Write frame of the clip."),
                new GUIContent("Interval", "Specifies the interval frame for creating keyframes."),
                new GUIContent("Root and IK Goal Curves", "It is used to prevent destruction of the ground feeling as much as possible with the function of Foot IK.\nSet it so that keyframes of some curves are not reduced."),
                new GUIContent("Simply apply the result of IK.\nUsed for baking the results of Foot IK and Animation Rigging."),
                new GUIContent("Based on the information at the time of Start and End, the value of the frame between is complemented.\nThis is the behavior of previous versions of Tools / Range IK."),
                new GUIContent("Compressed", "Enable compression.\nCannot be used at the same time that High Quality Curve is disabled."),
                new GUIContent("Use High Quality Curve", "The quality setting of the curve.\nIt cannot be disabled at the same time as Compressed."),
                //Settings
                new GUIContent("This will change the setting.\nSettings are saved in 'EditorPrefs'."),
                new GUIContent("Save Settings", "Save the 'VeryAnimationSaveSettings' component to the root game object."),
                new GUIContent("Default", "Basic operation of the Animation Window.\nThe 'Filter by selection' feature of the Animation Window since Unity 2019.2 works."),
                new GUIContent("Filter", "Only the information related to the selected bone is displayed.\nIf nothing is selected, all properties are displayed.\nThe 'Filter by selection' feature of the Animation Window for Unity 2019.2 and later will be disabled."),
                new GUIContent("Auto-run Frame All", "Frame All will be executed automatically when the information displayed is changed."),
                new GUIContent("Generic Scale Mirror", "Activate Scale operation with mirror operation.\nFor the mirror of the scale to work properly, the axis of rotation of the bone must be symmetrical."),
                new GUIContent("Search by Name", "Search for mirror targets by name."),
                new GUIContent("Ignore up to the specified character", "Ignore from the beginning to certain characters in the search by name."),
                new GUIContent("Synchronize Animation", "It is used to edit animations related to multiple characters.\nAll 'Animator' and 'Animation' except for editing target are played back in synchronization with time.\nIt is effective for editing while editor is stopped and not linked with Timeline."),
                new GUIContent("Onion Skinning", "It is a function to display the information before and after the current frame in a specified color."),
                new GUIContent("Trail of root position", "Display the trail of Root, which is the center of mass.\nIt is effective only with Humanoid."),
                new GUIContent("There is a setting change that becomes effective after restarting the editor."),
                //Help
                new GUIContent("This is other information.\nHotkeys works only when focused on Scene View or Editor Window."),
                new GUIContent("Default Shortcuts", "Some shortcut settings can be changed in the Shortcuts Window.\nShortcuts for operations related to the Animation Window use settings in the \"Animation\" category."),
                //Preview
                new GUIContent("This will display a preview.\nIt can be displayed with other models by drag and drop."),
                #endregion
                #region ControlGUI
                //AnimatorIK
                new GUIContent("Configure the IK using the function of Animator."),
                new GUIContent("Change All", "Change all targets"),
                new GUIContent("Select All", "Select all targets"),
                new GUIContent("The scale of this IK's parent bone has been changed from the default pose when Avatar was created.\nIf the scale is changed, Mecanim's IK does not work properly, so the IK position will shift.\nPlease return to the original scale."),
                new GUIContent("Animation Rigging/Enable", ""),
                new GUIContent("Animation Rigging/Disable", ""),
                //OriginalIK
                new GUIContent("Configure the IK that is specific to Very Animation.\nPlease select the bone which becomes the tip of IK and press the add button."),
                new GUIContent("Change All", "Change all targets"),
                new GUIContent("Select All", "Select all targets"),
                new GUIContent("Basic", "This is the basic CCD-IK."),
                new GUIContent("Limb", "Use it for limbs."),
                new GUIContent("LockAt", "It is used to direct it toward the target direction."),
                new GUIContent("Reset Rotations", "When it is enabled, it will be reset to the initial state before updating.\nAs a result, twisting will not occur, but the accuracy falls off as it deviates from the initial position."),
                new GUIContent("Direction", "Direction of bending"),
                new GUIContent("Please specify GameObject of the correct hierarchy."),
                new GUIContent("Set", "Set to list from original IK template file."),
                new GUIContent("Save as", "Name and save the current list as a original IK template file."),
                //Humanoid
                new GUIContent("This is the whole structure of Humanoid.\nHold down the Alt key and select, all children will also be selected."),
                //Selection
                new GUIContent("You can save and load the bones selected state with a name.\nPlease select the bones and press the plus button to add."),
                new GUIContent("Add", "Add to list from selection template file."),
                new GUIContent("Save as", "Name and save the current list as a selection template file."),
                new GUIContent("Move select", "Change selected bone from hierarchy."),
                //Hierarchy
                new GUIContent("Hold down the Alt key and change Toggle to change all children 's flags as well.\nHold down the Alt key and select, all children will also be selected."),
                new GUIContent("Humanoid/Humanoid Name", "It displays in Hierarchy under the name of Mecanim Humanoid."),
                new GUIContent("Mirror/Show Mirror Object", "Display Mirror object."),
                new GUIContent("Mirror/Mapping/Automap", "Mapping the Mirror object based on the setting."),
                new GUIContent("Mirror/Mapping/Clear", "Clear the setting of Mapping object."),
                new GUIContent("All", "Change all bones"),
                new GUIContent("Weight", "Change only bones with vertex weights"),
                new GUIContent("Body", "Change only Mecanim Humanoid Body"),
                new GUIContent("Face", "Change only Mecanim Humanoid Face"),
                new GUIContent("Left Hand", "Change only Mecanim Humanoid Left Hand"),
                new GUIContent("Right Hand", "Change only Mecanim Humanoid Right Hand"),
                new GUIContent("Renderer", "Change only the bone with the Renderer Component"),
                new GUIContent("Renderer Parent", "Change only the parent of the bone that has the Renderer Component"),
                #endregion
                #region EditorGUI
                //Common
                new GUIContent("On Scene", "Displays a window on the scene."),
                //Options
                new GUIContent("Clamp", "Limit Muscle to -1 to 1"),
                new GUIContent("Foot IK", "Automatically update Foot IK.\nFor operation in Timeline it is forcibly effective and works."),
                new GUIContent("Foot IK", "Automatically update Foot IK.\nIn the Timeline link state, it works with the Foot IK setting of Animation Playable Asset."),
                new GUIContent("Mirror", "Mirror left and right"),
                new GUIContent("Collision", "Make a collision decision between the mesh vertex being edited and the mesh triangle of the environment.\nIt is a very heavy process and should only be enabled if necessary.\nIt is recommended to use in .NET 4.x environment to enable acceleration of parallel processing."),
                //RootCorrection
                new GUIContent("Root Correction", "This is a function to automatically modify Root so that the whole positional relationship does not fluctuate when Muscle etc. are changed."),
                new GUIContent(VeryAnimation.RootCorrectionMode.Disable.ToString(), "No correction is made."),
                new GUIContent(VeryAnimation.RootCorrectionMode.Single.ToString(), "It corrects only the changed frame."),
                new GUIContent(VeryAnimation.RootCorrectionMode.Full.ToString(), "Corrects all frames that are affected before and after the change."),
                //Pose
                new GUIContent("Perform operations that affect the whole."),
                new GUIContent("Humanoid Default-Pose", "Mecanim Humanoid default pose"),
                new GUIContent("Avatar Configuration-Pose", "Avatar Configuration pose"),
                new GUIContent("Edit Start-Pose", "Edit start pose"),
                new GUIContent("T-Pose", "Set to T-Pose."),
                new GUIContent("Mesh Bind-Pose", "Mesh Bind Pose.\nInitial state poses of models such as T and A."),
                new GUIContent("Prefab-Pose", "Prefab Pose"),
                new GUIContent("Mirror", "Mirror left and right."),
                new GUIContent("Template", "Load from template file"),
                new GUIContent("Save as", "Save as template file"),
                new GUIContent(PoseTree.EditMode.All.ToString(), "Applies to all."),
                new GUIContent(PoseTree.EditMode.Selection.ToString(), "Applies only to selected bones."),
                //BlendPose
                new GUIContent("Blend the two poses.\nBy applying only a part, you can reuse facial expressions created and saved once, pose of fingers, etc."),
                new GUIContent("Please set the pose to the left and right."),
                new GUIContent(BlendPoseTree.EditMode.Tree.ToString(), "Applies only to the operated Node or below."),
                new GUIContent(BlendPoseTree.EditMode.Selection.ToString(), "Applies only to selected bones."),
                //MuscleGroup
                new GUIContent("This will manipulate the Muscle of Mecanim Humanoid collectively to a certain extent.\nApplies only to the operated Node or below."),
                //HandPose
                new GUIContent("It is a function specialized for the operation of only Muscle related to the finger from Muscle Group.\nYou can save poses and reuse them."),
                new GUIContent("Add", "Add to list from hand pose template file."),
                new GUIContent("Save as", "Name and save the current list as a hand pose template file."),
                //BlendShape
                new GUIContent("Manage all Mesh Blend Shapes.\nYou can save a set of all setting values and use them later."),
                new GUIContent("Mirror/Show Mirror Name", "Display Mirror name."),
                new GUIContent("Mirror/Mapping/Automap", "Mapping the Mirror name based on the setting."),
                new GUIContent("Mirror/Mapping/Clear", "Clear the setting of Mapping name."),
                new GUIContent("Add", "Add everything you can edit to the list, or from a blend shape template file."),
                new GUIContent("Save as", "Name and save the current list as a blend shape template file."),
                //Selection
                new GUIContent("Information on the currently selected object is displayed."),
                new GUIContent("No editing target"),
                new GUIContent("Can not edit because it conflicts with Humanoid"),
                new GUIContent("Nothing is selected"),
                new GUIContent("Mirror", "Mirror left and right.\n{0}"),
                new GUIContent("Update", "Calculate IK."),
                new GUIContent("Sync", "Synchronize IK information to the current state.\nIt cannot be used in the parent space."),
                new GUIContent("Reset", "Correct IK according to the part."),
                new GUIContent(AnimatorIKCore.AnimatorIKData.SpaceType.Global.ToString(), "Global space"),
                new GUIContent(AnimatorIKCore.AnimatorIKData.SpaceType.Local.ToString(), "Local space of IK root parent transform"),
                new GUIContent(AnimatorIKCore.AnimatorIKData.SpaceType.Parent.ToString(), "Local space of parent transform"),
                new GUIContent(OriginalIKCore.OriginalIKData.SpaceType.Global.ToString(), "Global space"),
                new GUIContent(OriginalIKCore.OriginalIKData.SpaceType.Local.ToString(), "Local space of IK root parent transform"),
                new GUIContent(OriginalIKCore.OriginalIKData.SpaceType.Parent.ToString(), "Local space of parent transform"),
                #endregion
                #region ToolsGUI
                new GUIContent("It does not work while editing VeryAnimation."),
                new GUIContent("Resets selected objects and children to a prefab Transform and BlendShape."),
                new GUIContent("Set Transform and BlendShape from the pose template file."),
                new GUIContent("Remove all 'VeryAnimationSaveSettings' component.\nPlease make a backup of the project."),
                new GUIContent("Replace animation clip reference in all AnimatorController, AnimatorOverrideController, Animation and Timeline.\nPlease make a backup of the project."),
                new GUIContent("Set the Animation Rigging Constraint.\nTo use it, you need to have Animation Rigging Package installed on Unity 2019.2 or later."),
	            #endregion
                #region Dialog
                new GUIContent("Need to save in the Assets folder", "You need to save the file inside of the project's assets floder"),
                new GUIContent("Remove Save Settings", "Remove all 'VeryAnimationSaveSettings' component.\nPlease make backup and then execute.\nAre you sure?"),
                new GUIContent("Replace Reference", "Replace all assets in the project.\nPlease make backup and then execute.\nAre you sure?"),
                #endregion
                #region Log
                new GUIContent("<color=blue>[Very Animation]</color>It can not update RootQ because of Euler Angles.\nPlease change to Quaternion with 'Tools/Rotation Curve Interpolation'."),
                new GUIContent("<color=blue>[Very Animation]</color>The animation clip is read-only. '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>This bone transform operation conflicts with the humanoid. Therefore, it can not operate. '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>This bone transform operation conflicts with the root motion. Therefore, it can not operate. '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>It is a name that cannot be used. The parameter name was changed. '{0}' -> '{1}'"),
                new GUIContent("<color=blue>[Very Animation]</color>There was a difference of more than 180 degrees in the key frames before and after, so the key was added for correct movement.\nThere may be a minute change in the animation curve. '{0} - {1}'"),
                new GUIContent("<color=blue>[Very Animation]</color>There must be at least two keyframes. If not, an Assert will occur.\nKeyframes were added for correction. '{0} - {1}'"),
                new GUIContent("<color=blue>[Very Animation]</color>The count of bones in SkinnedMeshRenderer and bindposes count of Mesh do not match. '{0}' != '{1}'"),
                new GUIContent("<color=blue>[Very Animation]</color>There is null in SkinnedMeshRenderer.bones.\nYou can not get BindPose with this incorrect setting.\nname = {0}, index = {1}"),
                new GUIContent("<color=blue>[Very Animation]</color>An attempt to acquire BindPose failed due to an unknown error.\nI guess that the state of SkinnedMeshRenderer is not normal."),
                new GUIContent("<color=blue>[Very Animation]</color>An attempt to acquire T-Pose failed due to an unknown error.\n"),
                new GUIContent("<color=blue>[Very Animation]</color>Failed to load Pose. '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>Target for curve is ambiguous since there are multiple GameObjects with same name.\nPlease change the name of GameObject. ({0})"),
                new GUIContent("<color=blue>[Very Animation]</color>The target is ambiguous because there are multiple same names in Mesh 's BlendShape.\nPlease change Mesh so that BlendShape does not overlap. (Mesh - {0}, Name - {1})"),
                new GUIContent("<color=blue>[Very Animation]</color>Bone '{0}' set in Avatar Humanoid has been deleted.\nCheck the Avatar settings."),
                new GUIContent("<color=blue>[Very Animation]</color>Failed to replace Animation Clip reference. '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>Failed to add Animation Clip. '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>The process of putting the Animation Window into the recording state failed for some reason.\nIf this problem reoccurs, reset the window layout or restart Unity."),
	            #endregion
            },
            #endregion
            #region Japanese
            new GUIContent[]
            {
                #region MainGUI
                new GUIContent("AnimationWindowが見つかりません。"),
                new GUIContent("AnimationWindowが前面に表示されていません。"),
                new GUIContent("AnimationWindowがリンクしているTimelineが有効ではありません。\nAnimationWindowのUnlinkボタンを押してリンクを解除して下さい。"),
                new GUIContent("アニメーション可能なオブジェクトが選択されていません。\n(Animator or Animation Component)"),
                new GUIContent("ComponentにAnimationClipが何も指定されていません。"),
                new GUIContent("GameObjectをActiveにする必要があります。"),
                new GUIContent("最適化されたTransform hierarchyの編集はサポートされていません。\nTransform hierarchyの最適化を解除して下さい。"),
                new GUIContent("Legacy(AnimationComponent)の実行中の編集はサポートされていません。"),
                new GUIContent("AnimatorControllerが存在しない場合の実行中の編集はサポートされていません。"),
                new GUIContent("AnimatorControllerが編集できません。"),
                new GUIContent("AnimationWindowがリンクしているAnimationTrackが編集できません。\nAnimationWindowのUnlinkボタンを押してリンクを解除して下さい。"),
                new GUIContent("Timelineの実行中の編集はサポートされていません。"),
                new GUIContent("TimelineのGameObjectをActiveにする必要があります。"),
                new GUIContent("Timeline Playable DirectorをEnableにする必要があります。"),
                new GUIContent("TimelineWindowが前面に表示されていません。"),
                new GUIContent("PrefabModeで編集するためにはPrefabModeのAutoSaveを無効にする必要があります。"),
                new GUIContent("PrefabModeとAnimation Windowで選択されたオブジェクトが一致しません。\nAnimation Windowのロックを解除して選択し直してください。"),
                new GUIContent("{0}が見つかりません。\n{1}のTranslation DoFは無効です。"),
                new GUIContent("このバージョンのUnityでの動作は未対応です。\nこのため正常に動作しない可能性があります。"),
                //Animation
                new GUIContent("編集対象のアニメーション情報です。"),
                new GUIContent("AnimationClipは読み取り専用です。\n編集可能なAnimationClipをAnimationWindowで選択して下さい。\nまたは右のボタンからこのAnimationClipの複製と参照の置換を行って下さい。"),
                new GUIContent("AnimationClip設定のLoop Pose (Blend)が有効になっています。\nこの状態ではアニメーションの終端に近いほど再生した場合の補正による差異が大きくなります。"),
                new GUIContent("AnimationClip設定のRoot Transform Position (Y)がFeetになっています。\nしかし、Foot IKのキーフレームが作成されていません。\n作成する必要があります。"),
                new GUIContent("AnimationClip設定のBased UponがOriginalでないまたはOffsetが0でない設定になっています。\nこの状態では再生で処理が追加されるため編集のSkeletonと位置がずれます。\nこれが意図的に設定したものでない場合には、Originalかつ0に変更して下さい。"),
                new GUIContent("AnimationClip設定のBake Into Poseが無効になっていて且つAnimatorのApply Root Motionも無効になっています。\nこの組み合わせでは再生した場合に意図しない挙動をする動作となります。\n編集を終了してからどちらかを変更して下さい。"),
                new GUIContent("AnimationClip設定のCycle Offsetが0ではありません。\nこの状態ではAnimator IKが正常な結果を取得できません。\n0にセットして下さい。"),
                new GUIContent("AnimationClip設定のMirrorが有効になっています。\nこの状態では正しく編集することが出来ません。\nMirrorを無効にして下さい。"),
                new GUIContent("TimelineとAnimationClipのフレームレートが一致していません。\n問題が発生するため同一に設定してください。\n Animation Clip {0} != Timeline {1}"),
                new GUIContent("Animation Playable Asset設定のRemove Start Offsetが有効になっています。\nこの状態では再生で処理が追加されるため編集のSkeletonと位置がずれます。\nこれが意図的に設定したものでない場合には、無効に変更して下さい。"),
                //Tools
                new GUIContent("様々なアニメーション関係の処理ツールです。"),
                new GUIContent("指定範囲のキーフレームとイベントをコピーします。"),
                new GUIContent("AnimationClipをトリミングします。"),
                new GUIContent("現在のAnimationClipの後ろに指定したClipの内容を追加します。"),
                new GUIContent("現在のAnimationClipへ指定したClipを合成します。\n重複するAnimationCurveは上書きされます。"),
                new GUIContent("新しいAnimationClipを作成します。\n現在のAnimationClip設定やAnimatorStateの状態もコピーされます。"),
                new GUIContent("選択しているボーンへ指定した間隔でキーフレームを作成します。"),
                new GUIContent("IKを指定の範囲実行します。\nOptionsの'Clamp'と'Foot IK'と'Mirror'の設定は結果に影響を及ぼします。"),
                new GUIContent("Animation Riggingのカーブを編集します。\n生成するためにはConstraintが設定されている必要があります。\n使用するにはUnity2019.2以降でAnimation Rigging Packageをインストールしている必要があります。"),
                new GUIContent("AnimatorStateのFoot IKが有効な場合に使用される情報を編集します。\nこれはHumanoidのみで有効です。"),
                new GUIContent("Root Motion情報を編集します。\nこの機能はHumanoidタイプとGenericタイプで使用可能です。"),
                new GUIContent("AnimationCurveの値からAnimatorControllerParameterの値へリレーする情報を編集します。\nFloatパラメータタイプのみが使用できます。\nHumanoidとGeneric Motionで有効です。"),
                new GUIContent("全てのTransform Rotation Curvesの補間モードを変更します。\nQuaternionモードを推奨します。"),
                new GUIContent("指定したエラー値でキーフレームの削減を実行します。\n設定項目についてはModelImporterのAnim.Compressionを参照してください。\nTransform Rotation Curvesの補間モードがQuaternionに統一されます。"),
                new GUIContent("AnimationClip.EnsureQuaternionContinuityを実行します。\nQuaternionカーブが編集されます。"),
                new GUIContent("AnimationClipから選択した情報のカーブを削除します。"),
                new GUIContent("エラーやアサートが発生する状態を修正します。\n通常は実行する必要はありません。"),
                new GUIContent("ビルドサイズを削減するためのアニメーション圧縮設定を変更します。\nanim作成時のデフォルト設定はCompressed無効、High Quality Curve有効です。\nModel Importerで設定することが不可能な組み合わせは使用できません。"),
                new GUIContent("Colladaファイルを出力します。\nColladaファイルは外部ツールでFBXファイルに変換することが可能です。"),
                new GUIContent("Humanoidタイプのみで有効です。"),
                new GUIContent("HumanoidタイプまたはGenericタイプのみで有効です。"),
                new GUIContent("Genericタイプ且つRoot nodeが指定されている場合のみ有効です。"),
                new GUIContent("この変換は逆回転などの問題を引き起こす可能性があります。"),
                new GUIContent("Write", "書き込み位置のフレーム"),
                new GUIContent("Interval", "キーフレームを作成する間隔フレームを指定します。"),
                new GUIContent("Root and IK Goal Curves", "Foot IKの機能で接地感をできるだけ破壊しないために使用します。\n一部のカーブのキーフレームを削減しないよう無効に設定します。"),
                new GUIContent("IKの結果を単純に適用します。\nFoot IKやAnimation Riggingの結果を焼き付けるために使用します。"),
                new GUIContent("StartとEndの時点の情報を元にして間のフレームの値は補完されます。\n旧バージョンのTools/Range IKの動作です。"),
                new GUIContent("Compressed", "圧縮を有効にします。\nHigh Quality Curveの無効と同時に使用できません。"),
                new GUIContent("Use High Quality Curve", "カーブの品質設定です。\nCompressedと同時には無効にできません。"),
                //Settings
                new GUIContent("設定を変更します。\n設定はEditorPrefsに保存されます。"),
                new GUIContent("Save Settings", "'VeryAnimationSaveSettings'コンポーネントをルートのGameObjectに作成します。"),
                new GUIContent("Default", "Animation Windowの基本的な動作です。\nUnity 2019.2以降のAnimation Windowの機能'Filter by selection'は動作します。"),
                new GUIContent("Filter", "選択したBoneに関係する情報のみを表示します。\n何も選択していない場合はすべてのプロパティが表示されます。\nUnity 2019.2以降のAnimation Windowの機能'Filter by selection'は無効になります。"),
                new GUIContent("Auto-run Frame All", "表示される情報の変更時に自動的にFrame Allを実行します。"),
                new GUIContent("Generic Scale Mirror", "ミラーの操作でScaleの操作を有効にします。\nスケールのミラーが正常に動作するには、ボーンの回転軸が対称である必要があります。"),
                new GUIContent("Search by Name", "ミラー対象を名前で探します。"),
                new GUIContent("Ignore up to the specified character", "名前での検索で最初から特定の文字までを無視します。"),
                new GUIContent("Synchronize Animation", "複数のキャラクターの関係するアニメーションの編集に使用します。\n編集対象以外の'Animator'と'Animation'のすべてを時間に同期して再生します。\nエディタ停止中でTimelineとリンクしていない状態での編集で有効です。"),
                new GUIContent("Onion Skinning", "現在のフレームの前後の情報を指定したカラーで重ねて表示する機能です。"),
                new GUIContent("Trail of root position", "質量の中心であるRootの軌跡を表示します。\nHumanoidのみで有効です。"),
                new GUIContent("エディタの再起動後に有効になる設定の変更があります。"),
                //Help
                new GUIContent("その他の情報です。\nショートカットキーはSceneViewかEditorWindowにフォーカスしている場合にのみ機能します。"),
                new GUIContent("Default Shortcuts", "一部のショートカット設定はShortcuts Windowで変更することが可能です。\nAnimation Windowに関係する操作のショートカットは\"Animation\"カテゴリーの設定を使用します。"),
                //Preivew
                new GUIContent("プレビューを表示します。\nドラッグアンドドロップで他のモデルの表示に切り替えることが出来ます。"),
                #endregion
                #region ControlGUI
                //AnimatorIK
                new GUIContent("Animatorの機能を使用したIKの機能を設定します。"),
                new GUIContent("Change All", "すべての対象を変更します。"),
                new GUIContent("Select All", "すべての対象を選択します。"),
                new GUIContent("Avatarが作成された時点のデフォルトポーズからこのIKの親のBoneのScaleが変更されています。\nScaleを変更するとMecanimのIKが正常に動作しないため、IKの位置のズレが発生します。\nScaleを確認して元に戻してください。"),
                new GUIContent("Animation Rigging/Enable", ""),
                new GUIContent("Animation Rigging/Disable", ""),
                //OriginalIK
                new GUIContent("Very Animation独自のIKを設定します。\n追加するにはIKの先端となるボーンを選択し+ボタンを押してください。"),
                new GUIContent("Change All", "すべての対象を変更します。"),
                new GUIContent("Select All", "すべての対象を選択します。"),
                new GUIContent("Basic", "基本的なCCD-IKタイプです。"),
                new GUIContent("Limb", "手足に使用するタイプです。"),
                new GUIContent("LockAt", "ターゲット方向に向かせるために使用します。"),
                new GUIContent("Reset Rotations", "有効にすると計算前に初期状態にリセットします。\nこれによりねじれは発生しませんが精度が低下します。"),
                new GUIContent("Direction", "曲がる方向の設定です。"),
                new GUIContent("GameObjectの指定にエラーがあります。\n正しいGameObjectと階層で指定して下さい。"),
                new GUIContent("Set", "独自のIKテンプレートファイルからリストを設定します。"),
                new GUIContent("Save as", "現在のリストを独自のIKテンプレートファイルに名前をつけて保存します。"),
                //Humanoid
                new GUIContent("Humanoidの全体構造です。\nAltキーを押しながら選択すると全ての子孫を選択することが出来ます。"),
                //Selection
                new GUIContent("ボーンの選択状態を名前をつけて保存して読み込むことができます。\n追加するにはボーンを選択し+ボタンを押してください。"),
                new GUIContent("Add", "選択テンプレートファイルからリストに追加します。"),
                new GUIContent("Save as", "現在のリストを選択テンプレートファイルに名前をつけて保存します。"),
                new GUIContent("Move select", "選択ボーンを階層から変更します。"),
                //Hierarchy
                new GUIContent("Altキーを押しながら変更することで全ての子孫のチェックを変更することが出来ます。\nAltキーを押しながら選択すると全ての子孫を選択することが出来ます。"),
                new GUIContent("Humanoid/Humanoid Name", "HierarchyでMecanim Humanoid制御のボーンをHumanoid名で表示します。"),
                new GUIContent("Mirror/Show Mirror Object", "Mirrorオブジェクトを表示します。"),
                new GUIContent("Mirror/Mapping/Automap", "Mirrorオブジェクトを設定を元にMappingします。"),
                new GUIContent("Mirror/Mapping/Clear", "Mappingオブジェクトの設定を消去します。"),
                new GUIContent("All", "全てのボーンを切り替えます。"),
                new GUIContent("Weight", "頂点にウェイトを持つボーンのみを切り替えます。"),
                new GUIContent("Body", "Mecanim HumanoidのBody部分のみを切り替えます。"),
                new GUIContent("Face", "Mecanim HumanoidのFace部分のみを切り替えます。"),
                new GUIContent("Left Hand", "Mecanim HumanoidのLeft Hand部分のみを切り替えます。"),
                new GUIContent("Right Hand", "Mecanim HumanoidのRight Hand部分のみを切り替えます。"),
                new GUIContent("Renderer", "Renderer Componentを持つボーンのみを切り替えます。"),
                new GUIContent("Renderer Parent", "Renderer Componentを持つボーンの親のみを切り替えます。"),
                #endregion
                #region EditorGUI
                //Common
                new GUIContent("On Scene", "シーン上にウィンドウを表示します。"),
                //Options
                new GUIContent("Clamp", "Muscleを-1から1の範囲に制限します。"),
                new GUIContent("Foot IK", "Foot IK情報を自動的に更新します。\nTimelineリンク状態では強制的に有効になります。"),
                new GUIContent("Foot IK", "Foot IK情報を自動的に更新します。\nTimelineリンク状態ではAnimation Playable AssetのFoot IK有効設定で動作します。"),
                new GUIContent("Mirror", "左右のミラーでの編集を行います。"),
                new GUIContent("Collision", "編集中のMeshの頂点と環境のMeshの三角形の間で衝突判定を行います。\nとても重い処理であるため必要な場合のみ有効にしてください。\n並列処理の高速化を有効にするため.NET 4.x環境での使用を推奨します。"),
                //RootCorrection
                new GUIContent("Root Correction", "Muscleなどを変更したときに全体の位置関係が変動しないように自動的にRootを補正する機能です。"),
                new GUIContent(VeryAnimation.RootCorrectionMode.Disable.ToString(), "補正を行いません。"),
                new GUIContent(VeryAnimation.RootCorrectionMode.Single.ToString(), "変更されたフレームのみ補正を行います。"),
                new GUIContent(VeryAnimation.RootCorrectionMode.Full.ToString(), "変更されたフレーム前後の影響があるすべてのフレームを補正します。"),
                //Pose
                new GUIContent("全体に影響する操作を行います。"),
                new GUIContent("Humanoid Default-Pose", "Mecanim Humanoidのデフォルトポーズにリセットします。"),
                new GUIContent("Avatar Configuration-Pose", "Avatar Configurationで設定したポーズに変更します。"),
                new GUIContent("Edit Start-Pose", "編集を開始した初期ポーズにリセットします。"),
                new GUIContent("T-Pose", "T-Poseに変更します。"),
                new GUIContent("Mesh Bind-Pose", "Meshのバインドポーズ情報からポーズを変更します。\nこれは主にTポーズのような初期状態のポーズです。"),
                new GUIContent("Prefab-Pose", "Prefabのポーズに変更します。"),
                new GUIContent("Mirror", "現在ポーズを左右反転したポーズに変更します。"),
                new GUIContent("Template", "ロードしたポーズテンプレートファイルのポーズに変更します。"),
                new GUIContent("Save as", "現在のポーズをポーズテンプレートファイルに名前をつけて保存します。"),
                new GUIContent(PoseTree.EditMode.All.ToString(), "すべてに適用されます。"),
                new GUIContent(PoseTree.EditMode.Selection.ToString(), "選択中のボーンのみに適用されます。"),
                //BlendPose
                new GUIContent("2つのポーズをブレンドします。\n全体ではなく表情や指など部分的に適用することで一度作成して保存したものを再利用できます。"),
                new GUIContent("左右にポーズをセットして下さい。"),
                new GUIContent(BlendPoseTree.EditMode.Tree.ToString(), "操作されたノード以下のみに適用されます。"),
                new GUIContent(BlendPoseTree.EditMode.Selection.ToString(), "選択中のボーンのみに適用されます。"),
                //MuscleGroup
                new GUIContent("Mecanim HumanoidのMuscleをまとめて操作します。\n操作されたノード以下のみに適用されます。"),
                //HandPose
                new GUIContent("Muscle Groupから手の指に関係するMuscleのみの操作に特化した機能です。\nポーズを保存して再利用することができます。"),
                new GUIContent("Add", "ハンドポーズテンプレートファイルからリストに追加します。"),
                new GUIContent("Save as", "現在のリストをハンドポーズテンプレートファイルに名前をつけて保存します。"),
                //BlendShape
                new GUIContent("すべてのMeshのBlendShapeを操作します。\nすべての設定値のセットを名前をつけて保存して後から使用することができます。"),
                new GUIContent("Mirror/Show Mirror Name", "Mirror名を表示します。"),
                new GUIContent("Mirror/Mapping/Automap", "Mirror名を設定を元にMappingします。"),
                new GUIContent("Mirror/Mapping/Clear", "Mapping名の設定を消去します。"),
                new GUIContent("Add", "編集可能なものをすべて、またはブレンドシェイプテンプレートファイルからリストに追加します。"),
                new GUIContent("Save as", "現在のリストをブレンドシェイプテンプレートファイルに名前をつけて保存します。"),
                //Selection
                new GUIContent("選択中オブジェクトの情報が表示されます。"),
                new GUIContent("編集対象がありません"),
                new GUIContent("Humanoidと競合するため編集できません。"),
                new GUIContent("何も選択されていません。"),
                new GUIContent("Mirror", "左右ミラー\n{0}"),
                new GUIContent("Update", "IKを計算し更新します。"),
                new GUIContent("Sync", "IKの情報を現在の状態に同期します。\n親空間では使用できません。"),
                new GUIContent("Reset", "オブジェクトに合わせてIKを修正します。"),
                new GUIContent(AnimatorIKCore.AnimatorIKData.SpaceType.Global.ToString(), "グローバル空間です。"),
                new GUIContent(AnimatorIKCore.AnimatorIKData.SpaceType.Local.ToString(), "IKルートの親オブジェクトのローカル空間です。"),
                new GUIContent(AnimatorIKCore.AnimatorIKData.SpaceType.Parent.ToString(), "指定した親オブジェクトのローカル空間です。"),
                new GUIContent(OriginalIKCore.OriginalIKData.SpaceType.Global.ToString(), "グローバル空間です。"),
                new GUIContent(OriginalIKCore.OriginalIKData.SpaceType.Local.ToString(), "IKルートの親オブジェクトのローカル空間です。"),
                new GUIContent(OriginalIKCore.OriginalIKData.SpaceType.Parent.ToString(), "指定した親オブジェクトのローカル空間です。"),
                #endregion
                #region ToolsGUI
                new GUIContent("VeryAnimation編集中には動作しません。"),
                new GUIContent("選択したオブジェクトと子孫のTransformとBlendShapeをPrefabの情報にリセットします。"),
                new GUIContent("ポーズテンプレートファイルからTransformとBlendShapeをセットします。"),
                new GUIContent("プロジェクトのすべてのVeryAnimationSaveSettings Componentを削除します。\nプロジェクトのバックアップを作成してから実行して下さい。"),
                new GUIContent("プロジェクトの全てのAnimatorController, AnimatorOverrideController, Animation, TimelineのAnimationClip参照を置き換えます。\nプロジェクトのバックアップを作成してから実行して下さい。"),
                new GUIContent("Animation Rigging Constraintを設定します。\n使用するにはUnity2019.2以降でAnimation Rigging Packageをインストールしている必要があります。"),
	            #endregion
                #region Dialog
                new GUIContent("Assetsフォルダ内に保存する必要があります", "プロジェクトのAssetsフォルダ内を指定してください。"),
                new GUIContent("設定のセーブの削除", "プロジェクト内のすべての'VeryAnimationSaveSettings'コンポーネントを削除します。\nバックアップを作成してから実行してください。\nよろしいですか？"),
                new GUIContent("参照の置換", "プロジェクト内のすべての参照を置き換えます。\nバックアップを作成してから実行してください。\nよろしいですか？"),
                #endregion
                #region Log
                new GUIContent("<color=blue>[Very Animation]</color>RootQをRotation Curve InterpolationがEuler Anglesであるため更新ができません。\n'Tools/Rotation Curve Interpolation'を使用してQuaternionに変更してください。"),
                new GUIContent("<color=blue>[Very Animation]</color>このAnimationClipは読み取り専用です。 '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>このボーンのTransformの操作はHumanoidの管理と競合するため操作できません。 '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>このボーンのTransformの操作はルートモーションと競合するため操作できません。 '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>指定した名前は使用できないためパラメータ名が変更されました。 '{0}' -> '{1}'"),
                new GUIContent("<color=blue>[Very Animation]</color>前後のキーフレームには180度以上の違いがあったため、正しい動きのためにキーが追加されました。\nAnimationCurveにはわずかな変更があります。 '{0} - {1}'"),
                new GUIContent("<color=blue>[Very Animation]</color>少なくとも2つのキーフレームが必要です。 そうでない場合、Assertが発生します。\n修正のためにキーフレームが追加されました。 '{0} - {1}'"),
                new GUIContent("<color=blue>[Very Animation]</color>SkinnedMeshRendererのbonesとMeshのbindposesの数が一致しません。 '{0}' != '{1}'"),
                new GUIContent("<color=blue>[Very Animation]</color>SkinnedMeshRenderer.bonesの中にnullがあります。\nこの状態ではBindPoseを取得できません。\nname = {0}, index = {1}"),
                new GUIContent("<color=blue>[Very Animation]</color>不明なエラーによってBindPoseの取得に失敗しました。\nSkinnedMeshRendererの状態が正常ではないと推測します。"),
                new GUIContent("<color=blue>[Very Animation]</color>不明なエラーによってTPoseの取得に失敗しました。"),
                new GUIContent("<color=blue>[Very Animation]</color>Poseのロードに失敗しました。 '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>同じ階層に同じ名前のGameObjectが複数存在するため、AnimationCurveのターゲットが曖昧です。\nGameObjectの名前を変更してください。 ({0})"),
                new GUIContent("<color=blue>[Very Animation]</color>MeshのBlendShapeに同じ名前が複数存在するため対象が曖昧です。\nBlendShapeは重複しない状態になるようにMeshを変更してください。 (Mesh - {0}, Name - {1})"),
                new GUIContent("<color=blue>[Very Animation]</color>AvatarのHumanoidに設定されたBoneの'{0}'が削除されています。\nAvatarの設定を確認してください。"),
                new GUIContent("<color=blue>[Very Animation]</color>AnimationClipの参照の置き換えに失敗しました。 '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>AnimationClipの追加に失敗しました。 '{0}'"),
                new GUIContent("<color=blue>[Very Animation]</color>AnimationWindowを録画状態にする処理が何らかの理由で失敗しました。\nこの問題が再現する場合にはウィンドウのレイアウトをリセットするかUnityを再起動してください。"),
	            #endregion
            },
            #endregion
        };

        public static string GetText(Help help)
        {
            return LanguageHelps[(int)languageType][(int)help].text;
        }
        public static string GetTooltip(Help help)
        {
            return LanguageHelps[(int)languageType][(int)help].tooltip;
        }
        public static GUIContent GetContent(Help help)
        {
            return LanguageHelps[(int)languageType][(int)help];
        }
        public static GUIContent GetContentFormat(Help help, params object[] tooltipArgs)
        {
            var content = new GUIContent(LanguageHelps[(int)languageType][(int)help]);
            content.tooltip = String.Format(content.tooltip, tooltipArgs);
            return content;
        }

        public static Action OnLanguageChanged;
    }
}
