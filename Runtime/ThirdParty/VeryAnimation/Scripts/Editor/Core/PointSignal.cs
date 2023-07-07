using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace VeryAnimation
{
    public class PointSignal
    {
        private UHandleUtility uHandleUtility;

        private class Signal
        {
            public Vector3 position;
            public DateTime startTime;
            public DateTime endTime;
            public Color color;
        }
        private List<Signal> signals;

        public PointSignal()
        {
            uHandleUtility = new UHandleUtility();
            signals = new List<Signal>();
        }

        public void Fire(Vector3 position, float seconds, Color color)
        {
            var now = DateTime.UtcNow;
            signals.Add(new Signal()
            {
                position = position,
                startTime = now,
                endTime = now.AddSeconds(seconds),
                color = color,
            });
        }

        public bool Draw()
        {
            if (signals.Count <= 0)
                return false;

            var now = DateTime.UtcNow;
            for (int i = 0; i < signals.Count; i++)
            {
                var rate = 1f - (signals[i].endTime - now).TotalSeconds / (signals[i].endTime - signals[i].startTime).TotalSeconds;
                if (rate < 0f || rate > 1f)
                {
                    signals.RemoveAt(i--);
                    continue;
                }

                var color = signals[i].color;
                color.a = Mathf.Lerp(color.a, 0f, (float)rate);

                Vector3 up, right;
                {
                    const float Size = 0.1f;
                    float offset = HandleUtility.GetHandleSize(signals[i].position) * Size;
                    var transform = SceneView.currentDrawingSceneView.camera.transform;
                    up = transform.up * offset;
                    right = transform.right * offset;
                }

                {
                    uHandleUtility.ApplyWireMaterial();
                    GL.PushMatrix();
                    GL.MultMatrix(Handles.matrix);
                    GL.Begin(GL.LINES);
                    GL.Color(color);
                    {
                        GL.Vertex(signals[i].position - up - right);
                        GL.Vertex(signals[i].position + up + right);

                        GL.Vertex(signals[i].position - up + right);
                        GL.Vertex(signals[i].position + up - right);
                    }
                    GL.End();
                    GL.PopMatrix();
                }
            }

            return true;
        }
    }
}
