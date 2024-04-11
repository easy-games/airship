using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class RootTrail
    {
        private VeryAnimationWindow vaw { get { return VeryAnimationWindow.instance; } }
        private VeryAnimation va { get { return VeryAnimation.instance; } }

        private AnimationCurve[] curves;
        private Vector3[] trailPositions;

        public RootTrail()
        {
            curves = new AnimationCurve[3];
        }

        internal void Draw()
        {
            if (!va.isHuman) return;

            #region CurveReady
            for (int dof = 0; dof < 3; dof++)
            {
                curves[dof] = va.GetAnimationCurveAnimatorRootT(dof, false);
                if (curves[dof] == null) return;
            }
            #endregion

            var lastFrame = va.GetLastFrame();
            if (trailPositions == null || trailPositions.Length != lastFrame + 1)
                trailPositions = new Vector3[lastFrame + 1];

            var matrix = va.transformPoseSave.startMatrix;

            vaw.uHandleUtility.ApplyWireMaterial();
            GL.PushMatrix();
            GL.MultMatrix(Handles.matrix);
            GL.Begin(GL.LINE_STRIP);
            GL.Color(vaw.editorSettings.settingExtraRootTrailColor);
            {
                var beforeTime = 0f;
                var beforePos = Vector3.zero;
                for (int frame = 0; frame <= lastFrame; frame++)
                {
                    var time = va.GetFrameTime(frame);
                    var pos = matrix.MultiplyPoint3x4(GetCurveValue(time) * va.editAnimator.humanScale);

                    if (frame > 0)
                    {
                        const float Granularity = 0.04f;
                        const int MaxCount = 64;
                        var screenLength = Vector2.Distance(HandleUtility.WorldToGUIPoint(beforePos), HandleUtility.WorldToGUIPoint(pos));
                        int count = Math.Min(Mathf.RoundToInt(screenLength * Granularity), MaxCount);
                        var step = 1f / (count + 1f);
                        for (int i = 0; i < count; i++)
                        {
                            var rate = step * (i + 1);
                            var stepTime = Mathf.Lerp(beforeTime, time, rate);
                            var stepPos = matrix.MultiplyPoint3x4(GetCurveValue(stepTime) * va.editAnimator.humanScale);
                            GL.Vertex(stepPos);
                        }
                    }
                    GL.Vertex(pos);

                    beforeTime = time;
                    beforePos = pos;
                }
            }
            GL.End();
            GL.PopMatrix();
        }

        private Vector3 GetCurveValue(float time)
        {
            var pos = Vector3.zero;
            for (int dof = 0; dof < 3; dof++)
                pos[dof] = curves[dof].Evaluate(time);
            return pos;
        }
    }
}
