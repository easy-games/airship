using UnityEngine;
using UnityEditor;
using System;
#if UNITY_2019_1_OR_NEWER
using UnityEditor.ShortcutManagement;
#endif

namespace VeryAnimation
{
    public static class Shortcuts
    {
#if UNITY_2019_1_OR_NEWER
        public const string changeClamp = "Very Animation/Editor/Change Clamp";
        [Shortcut(changeClamp, typeof(VeryAnimationEditorWindow), KeyCode.O)]
        static void ChangeClamp(ShortcutArguments args) { }

        public const string changeFootIK = "Very Animation/Editor/Change Foot IK";
        [Shortcut(changeFootIK, typeof(VeryAnimationEditorWindow), KeyCode.J)]
        static void ChangeFootIK(ShortcutArguments args) { }

        public const string changeMirror = "Very Animation/Editor/Change Mirror";
        [Shortcut(changeMirror, typeof(VeryAnimationEditorWindow), KeyCode.M)]
        static void ChangeMirror(ShortcutArguments args) { }

        public const string changeCollision = "Very Animation/Editor/Change Collision";
        [Shortcut(changeCollision, typeof(VeryAnimationEditorWindow), KeyCode.N)]
        static void ChangeCollision(ShortcutArguments args) { }

        public const string changeRootCorrectionMode = "Very Animation/Editor/Change Root Correction Mode";
        [Shortcut(changeRootCorrectionMode, typeof(VeryAnimationEditorWindow), KeyCode.L)]
        static void ChangeRootCorrectionMode(ShortcutArguments args) { }

        public const string changeSelectionBoneIK = "Very Animation/Editor/Change selection bone IK";
        [Shortcut(changeSelectionBoneIK, typeof(VeryAnimationEditorWindow), KeyCode.I)]
        static void ChangeSelectionBoneIK(ShortcutArguments args) { }

        public const string forceRefresh = "Very Animation/Animation Window/Force refresh";
        [Shortcut(forceRefresh, typeof(VeryAnimationEditorWindow), KeyCode.F5)]
        static void ForceRefresh(ShortcutArguments args) { }

        public const string nextAnimationClip = "Very Animation/Animation Window/Next animation clip";
        [Shortcut(nextAnimationClip, typeof(VeryAnimationEditorWindow), KeyCode.PageDown)]
        static void NextAnimationClip(ShortcutArguments args) { }

        public const string previousAnimationClip = "Very Animation/Animation Window/Previous animation clip";
        [Shortcut(previousAnimationClip, typeof(VeryAnimationEditorWindow), KeyCode.PageUp)]
        static void PreviousAnimationClip(ShortcutArguments args) { }

        public const string addInbetween = "Very Animation/Animation Window/Edit Keys/Add In between";
        [Shortcut(addInbetween, typeof(VeryAnimationEditorWindow), KeyCode.KeypadPlus, ShortcutModifiers.None)]
        static void AddInbetween(ShortcutArguments args) { }

        public const string removeInbetween = "Very Animation/Animation Window/Edit Keys/Remove In between";
        [Shortcut(removeInbetween, typeof(VeryAnimationEditorWindow), KeyCode.KeypadMinus, ShortcutModifiers.None)]
        static void RemoveInbetween(ShortcutArguments args) { }

        public const string hideSelectBones = "Very Animation/Hierarchy/Hide select bones";
        [Shortcut(hideSelectBones, typeof(VeryAnimationEditorWindow), KeyCode.H)]
        static void HideSelectBones(ShortcutArguments args) { }

        public const string showSelectBones = "Very Animation/Hierarchy/Show select bones";
        [Shortcut(showSelectBones, typeof(VeryAnimationEditorWindow), KeyCode.H, ShortcutModifiers.Shift)]
        static void ShowSelectBones(ShortcutArguments args) { }

        public const string previewChangePlaying = "Very Animation/Preview/Change playing";
        [Shortcut(previewChangePlaying, typeof(VeryAnimationEditorWindow),
#if UNITY_EDITOR_OSX
            KeyCode.Space, ShortcutModifiers.Alt)]
#else
            KeyCode.Space, ShortcutModifiers.Action)]
#endif
        static void PreviewChangePlaying(ShortcutArguments args) { }

        public const string addIKLevel = "Very Animation/IK/Add IK - Level / Direction";
        [Shortcut(addIKLevel, typeof(VeryAnimationEditorWindow), KeyCode.KeypadPlus, ShortcutModifiers.Action)]
        static void AddIKLevel(ShortcutArguments args) { }

        public const string subIKLevel = "Very Animation/IK/Sub IK - Level / Direction";
        [Shortcut(subIKLevel, typeof(VeryAnimationEditorWindow), KeyCode.KeypadMinus, ShortcutModifiers.Action)]
        static void SubIKLevel(ShortcutArguments args) { }

        private static bool EqualKeyCombinationSequence(string id, Event e)
        {
            foreach (var key in ShortcutManager.instance.GetShortcutBinding(id).keyCombinationSequence)
            {
                if (key.action == IsKeyControl(e) && key.alt == e.alt && key.shift == e.shift && key.keyCode == e.keyCode)
                    return true;
            }
            return false;
        }
#endif

        public static bool IsKeyControl(Event e)
        {
#if UNITY_EDITOR_OSX
            return e.command;
#else
            return e.control;
#endif
        }

        #region Very Animation Shortcuts
        public static bool IsChangeClamp(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(changeClamp, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.O;
#endif
        }

        public static bool IsChangeFootIK(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(changeFootIK, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.J;
#endif
        }

        public static bool IsChangeMirror(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(changeMirror, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.M;
#endif
        }

        public static bool IsChangeCollision(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(changeCollision, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.N;
#endif
        }

        public static bool IsChangeRootCorrectionMode(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(changeRootCorrectionMode, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.L;
#endif
        }

        public static bool IsChangeSelectionBoneIK(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(changeSelectionBoneIK, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.I;
#endif
        }

        public static bool IsForceRefresh(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(forceRefresh, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.F5;
#endif
        }

        public static bool IsNextAnimationClip(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(nextAnimationClip, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.PageDown;
#endif
        }

        public static bool IsPreviousAnimationClip(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(previousAnimationClip, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.PageUp;
#endif
        }

        public static bool IsAddInbetween(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(addInbetween, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.KeypadPlus;
#endif
        }

        public static bool IsRemoveInbetween(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(removeInbetween, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.KeypadMinus;
#endif
        }

        public static bool IsHideSelectBones(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(hideSelectBones, e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.H;
#endif
        }

        public static bool IsShowSelectBones(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(showSelectBones, e);
#else
            return !IsKeyControl(e) && !e.alt && e.shift && e.keyCode == KeyCode.H;
#endif
        }

        public static bool IsPreviewChangePlaying(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(previewChangePlaying, e);
#else
#if UNITY_EDITOR_OSX
            return !IsKeyControl(e) && e.alt && !e.shift && e.keyCode == KeyCode.Space;
#else
            return IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.Space;
#endif
#endif
        }

        public static bool IsAddIKLevel(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(addIKLevel, e);
#else
            return IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.KeypadPlus;
#endif
        }

        public static bool IsSubIKLevel(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence(subIKLevel, e);
#else
            return IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.KeypadMinus;
#endif
        }
        #endregion

        #region Animation Window Shortcuts
        public static bool IsAnimationChangePlaying(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence("Animation/Play Animation", e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.Space;
#endif
        }

        public static bool IsAnimationSwitchBetweenCurvesAndDopeSheet(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence("Animation/Show Curves", e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.C;
#endif
        }

        public static bool IsAddKeyframe(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence("Animation/Key Selected", e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.K;
#endif
        }

        public static bool IsMoveToNextFrame(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence("Animation/Next Frame", e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.Period;
#endif
        }

        public static bool IsMoveToPrevFrame(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence("Animation/Previous Frame", e);
#else
            return !IsKeyControl(e) && !e.alt && !e.shift && e.keyCode == KeyCode.Comma;
#endif
        }

        public static bool IsMoveToNextKeyframe(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence("Animation/Next Keyframe", e);
#else
            return !IsKeyControl(e) && e.alt && !e.shift && e.keyCode == KeyCode.Period;
#endif
        }

        public static bool IsMoveToPreviousKeyframe(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence("Animation/Previous Keyframe", e);
#else
            return !IsKeyControl(e) && e.alt && !e.shift && e.keyCode == KeyCode.Comma;
#endif
        }

        public static bool IsMoveToFirstKeyframe(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence("Animation/First Keyframe", e);
#else
            return !IsKeyControl(e) && !e.alt && e.shift && e.keyCode == KeyCode.Comma;
#endif
        }

        public static bool IsMoveToLastKeyframe(Event e)
        {
#if UNITY_2019_1_OR_NEWER
            return EqualKeyCombinationSequence("Animation/Last Keyframe", e);
#else
            return !IsKeyControl(e) && !e.alt && e.shift && e.keyCode == KeyCode.Period;
#endif
        }
        #endregion
    }
}
