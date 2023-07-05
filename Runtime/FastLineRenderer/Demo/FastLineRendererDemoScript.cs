using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System;

namespace DigitalRuby.FastLineRenderer
{
    /// <summary>
    /// Fast line renderer demo script with lots of different functions
    /// </summary>
    [ExecuteInEditMode]
    public class FastLineRendererDemoScript : MonoBehaviour
    {
        /// <summary>
        /// Fast line renderer script
        /// </summary>
        public FastLineRenderer LineRenderer;

        /// <summary>
        /// Unity line renderer script
        /// </summary>
        public LineRenderer UnityLineRenderer;

        /// <summary>
        /// Toggle for unity vs fast line renderer
        /// </summary>
        public Toggle UseUnityLineRendererToggle;

        /// <summary>
        /// Line count label
        /// </summary>
        public Text LineCountLabel;

        /// <summary>
        /// Move speed
        /// </summary>
        public float MoveSpeed = 50.0f;

        /// <summary>
        /// Enable mouse look
        /// </summary>
        public bool EnableMouseLook;

        /// <summary>
        /// Show curves
        /// </summary>
        public bool ShowCurves;

        /// <summary>
        /// Show effects
        /// </summary>
        public bool ShowEffects;

        /// <summary>
        /// Show grid
        /// </summary>
        public bool ShowGrid;

        private List<Vector3> UnityLineRendererPositions = new List<Vector3>();
        private float deltaTime = 0.0f;
        private float msec = 0.0f;
        private float fps = 0.0f;
        private int lineCount;

        private enum RotationAxes { MouseXAndY = 0, MouseX = 1, MouseY = 2 }
        private RotationAxes axes = RotationAxes.MouseXAndY;
        private float sensitivityX = 15F;
        private float sensitivityY = 15F;
        private float minimumX = -360F;
        private float maximumX = 360F;
        private float minimumY = -60F;
        private float maximumY = 60F;
        private float rotationX = 0F;
        private float rotationY = 0F;
        private Quaternion originalRotation;

        private void UpdateMovement()
        {
            if (Camera.main.orthographic)
            {
                return;
            }

            float speed = MoveSpeed * Time.deltaTime;

            if (Input.GetKey(KeyCode.W))
            {
                Camera.main.transform.Translate(0.0f, 0.0f, speed);
            }
            if (Input.GetKey(KeyCode.S))
            {
                Camera.main.transform.Translate(0.0f, 0.0f, -speed);
            }
            if (Input.GetKey(KeyCode.A))
            {
                Camera.main.transform.Translate(-speed, 0.0f, 0.0f);
            }
            if (Input.GetKey(KeyCode.D))
            {
                Camera.main.transform.Translate(speed, 0.0f, 0.0f);
            }
        }

        private void UpdateMouseLook()
        {
            if (Camera.main.orthographic || !EnableMouseLook)
            {
                return;
            }
            else if (axes == RotationAxes.MouseXAndY)
            {
                // Read the mouse input axis
                rotationX += Input.GetAxis("Mouse X") * sensitivityX;
                rotationY += Input.GetAxis("Mouse Y") * sensitivityY;

                rotationX = ClampAngle(rotationX, minimumX, maximumX);
                rotationY = ClampAngle(rotationY, minimumY, maximumY);

                Quaternion xQuaternion = Quaternion.AngleAxis(rotationX, Vector3.up);
                Quaternion yQuaternion = Quaternion.AngleAxis(rotationY, -Vector3.right);

                transform.localRotation = originalRotation * xQuaternion * yQuaternion;
            }
            else if (axes == RotationAxes.MouseX)
            {
                rotationX += Input.GetAxis("Mouse X") * sensitivityX;
                rotationX = ClampAngle(rotationX, minimumX, maximumX);

                Quaternion xQuaternion = Quaternion.AngleAxis(rotationX, Vector3.up);
                transform.localRotation = originalRotation * xQuaternion;
            }
            else
            {
                rotationY += Input.GetAxis("Mouse Y") * sensitivityY;
                rotationY = ClampAngle(rotationY, minimumY, maximumY);

                Quaternion yQuaternion = Quaternion.AngleAxis(-rotationY, Vector3.right);
                transform.localRotation = originalRotation * yQuaternion;
            }
        }

        private void OnGUI()
        {
            if (deltaTime == 0.0f)
            {
                return;
            }

            int w = Screen.width, h = Screen.height;

            GUIStyle style = new GUIStyle();

            Rect rect = new Rect(12, h - 42, w, h * 2 / 100);
            style.alignment = TextAnchor.UpperLeft;
            style.fontSize = 30;
            style.normal.textColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);

            msec = deltaTime * 1000.0f;
            fps = 1.0f / deltaTime;

            string text = string.Format("{0:0.0} ms ({1:0.} fps)", msec, fps);
            GUI.Label(rect, text, style);
        }

        private byte RandomByte()
        {
            return (byte)UnityEngine.Random.Range(0, byte.MaxValue);
        }

        private Vector3 RandomVelocity(float c)
        {
            return new Vector3(UnityEngine.Random.Range(-c, c), UnityEngine.Random.Range(-c, c), UnityEngine.Random.Range(-c, c));
        }

        private void DoShowEffects()
        {
            FastLineRendererProperties props = new FastLineRendererProperties();
            FastLineRenderer r = FastLineRenderer.CreateWithParent(null, LineRenderer);
            r.Material.EnableKeyword("DISABLE_CAPS");
            r.SetCapacity(8192 * FastLineRenderer.VerticesPerLine);
            r.Turbulence = 350.0f;
            r.BoundsScale = new Vector3(4.0f, 4.0f, 4.0f);
            //r.JitterMultiplier = UnityEngine.Random.Range(0.25f, 1.5f);

            const float maxLifeTimeSeconds = 1.5f;

            for (int i = 0; i < 32; i++)
            {
                Vector3 pos = new Vector3(10.0f, Screen.height * 0.5f);
                Vector3 pos2 = new Vector3(10.0f + (UnityEngine.Random.Range(50.0f, 150.0f)), Screen.height * 0.5f + (UnityEngine.Random.Range(-15.0f, 15.0f)));
                props.Start = pos;
                props.End = pos2;
                props.Radius = UnityEngine.Random.Range(8.0f, 16.0f);
                float s = UnityEngine.Random.Range(1.0f, maxLifeTimeSeconds);
                props.SetLifeTime(s, s * UnityEngine.Random.Range(0.1f, 0.2f));
                props.Color = new Color32(RandomByte(), RandomByte(), RandomByte(), RandomByte());
                props.Velocity = RandomVelocity(20.0f);
                props.Velocity.z = 0.0f;
                props.AngularVelocity = UnityEngine.Random.Range(-0.1f, 0.1f);
                r.AddLine(props);
            }
            r.Apply();

            // send the script back into the cache, freeing up resources after max lifetime seconds.
            r.SendToCacheAfter(TimeSpan.FromSeconds(maxLifeTimeSeconds));
        }

        private void DoGrid()
        {
            FastLineRendererProperties props = new FastLineRendererProperties
            {
                Radius = 2.0f
            };
            Bounds gridBounds = new Bounds();

            // *** Note: For a 2D grid, pass a value of true for the fill parameter for optimization purposes ***

            // draw a grid cube without filling
            gridBounds.SetMinMax(new Vector3(-2200.0f, -1000.0f, 1000.0f), new Vector3(-200.0f, 1000.0f, 3000.0f));
            LineRenderer.AppendGrid(props, gridBounds, 250, false);

            // draw a grid cube with filling
            gridBounds.SetMinMax(new Vector3(200.0f, -1000.0f, 1000.0f), new Vector3(2200.0f, 1000.0f, 3000.0f));
            LineRenderer.AppendGrid(props, gridBounds, 250, true);

            // commit the changes
            LineRenderer.Apply();
        }

        private void UpdateDynamicLines()
        {
            if (!Input.GetKeyDown(KeyCode.Space) || LineRenderer == null)
            {
                return;
            }
            else if (ShowEffects)
            {
                DoShowEffects();
                return;
            }

            const float maxLifeTimeSeconds = 5.0f;
            FastLineRendererProperties props = new FastLineRendererProperties();
            FastLineRenderer r = FastLineRenderer.CreateWithParent(null, LineRenderer);
            r.Material.EnableKeyword("DISABLE_CAPS");
            r.SetCapacity(8192 * FastLineRenderer.VerticesPerLine);
            r.Turbulence = 150.0f;
            r.BoundsScale = new Vector3(4.0f, 4.0f, 4.0f);
            props.GlowIntensityMultiplier = 0.1f;
            props.GlowWidthMultiplier = 4.0f;

            for (int i = 0; i < 8192; i++)
            {
                Vector3 pos;
                if (Camera.main.orthographic)
                {
                    pos = UnityEngine.Random.insideUnitCircle;
                    pos.x *= Screen.width * UnityEngine.Random.Range(0.1f, 0.4f);
                    pos.y *= Screen.height * UnityEngine.Random.Range(0.1f, 0.4f);
                }
                else
                {
                    pos = UnityEngine.Random.insideUnitSphere;
                    pos.x *= Screen.width * UnityEngine.Random.Range(0.5f, 2.0f);
                    pos.y *= Screen.height * UnityEngine.Random.Range(0.5f, 2.0f);
                    pos.z *= UnityEngine.Random.Range(-200.0f, 200.0f);
                }
                props.End = pos;
                props.Radius = UnityEngine.Random.Range(1.0f, 4.0f);
                float s = UnityEngine.Random.Range(1.0f, maxLifeTimeSeconds);
                props.SetLifeTime(s, s * 0.15f);
                props.Color = new Color32(RandomByte(), RandomByte(), RandomByte(), RandomByte());
                props.Velocity = RandomVelocity(100.0f);
                props.AngularVelocity = UnityEngine.Random.Range(-6.0f, 6.0f);
                r.AddLine(props);
            }
            r.Apply();
            r.SendToCacheAfter(TimeSpan.FromSeconds(maxLifeTimeSeconds));
        }

        private void CheckInput()
        {
            UpdateMovement();
            UpdateMouseLook();
            UpdateDynamicLines();
        }

        private void Start()
        {
            originalRotation = transform.localRotation;

            if (UnityLineRenderer != null)
            {
                UnityLineRenderer.startColor = Color.green;
                UnityLineRenderer.endColor = Color.blue; 
            }

            AddCurvesAndSpline();
            if (ShowGrid)
            {
                DoGrid();
            }
        }

        private void Update()
        {
            deltaTime += (Time.deltaTime - deltaTime) * 0.1f;

            if (Camera.main.orthographic)
            {
                // 1 pixel = 1 world unit
                Camera.main.orthographicSize = Screen.height * 0.5f;
                Camera.main.transform.position = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, -10.0f);
            }

            CheckInput();
        }

        private void AddCurvesAndSpline()
        {
            if (ShowCurves && Application.isPlaying && LineRenderer != null)
            {
                const float animationTime = 0.025f;

                ShowCurves = false;
                FastLineRendererProperties props = new FastLineRendererProperties();
                props.GlowIntensityMultiplier = 0.5f;
                props.Radius = 4.0f;
                props.Color = UnityEngine.Color.cyan;
                props.Start = new Vector3(Screen.width * 0.1f, Screen.height * 0.1f, 0.0f);
                props.End = new Vector3(Screen.width * 1.1f, Screen.height * 1.0f, 0.0f);
                LineRenderer.AppendCurve(props,
                    new Vector3(Screen.width * 0.33f, Screen.height * 0.67f, 0.0f), // control point 1
                    new Vector3(Screen.width * 0.67f, Screen.height * 0.33f, 0.0f), // control point 2
                    16, true, true, animationTime);

                props.Color = UnityEngine.Color.red;
                props.Start = new Vector3(0.0f, Screen.height * 0.2f, 0.0f);
                props.End = new Vector3(Screen.width * 1.2f, Screen.height * 0.2f, 0.0f);

                Vector3[] spline = new Vector3[]
                {
                    props.Start,
                    new Vector3(Screen.width * 0.2f, Screen.height * 0.8f, 0.0f),
                    new Vector3(Screen.width * 0.4f, Screen.height * 0.2f, 0.0f),
                    new Vector3(Screen.width * 0.6f, Screen.height * 0.8f, 0.0f),
                    new Vector3(Screen.width * 0.8f, Screen.height * 0.2f, 0.0f),
                    new Vector3(Screen.width, Screen.height * 0.8f, 0.0f),
                    props.End
                };
                LineRenderer.AppendSpline(props, spline, 128, FastLineRendererSplineFlags.StartCap | FastLineRendererSplineFlags.EndCap, animationTime);

                // add a circle and arc
                props.Color = Color.green;
                LineRenderer.AppendCircle(props, new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0.0f), 100.0f, 64, Vector3.forward, animationTime);
                LineRenderer.AppendArc(props, new Vector3(Screen.width * 0.25f, Screen.height * 0.5f, 0.0f), 100.0f, 270.0f, 90.0f, 32, Vector3.forward, false, animationTime);
                LineRenderer.AppendArc(props, new Vector3(Screen.width * 0.75f, Screen.height * 0.5f, 0.0f), 100.0f, 0.0f, 360.0f, 32, Vector3.forward, false, animationTime);

                LineRenderer.Apply();
            }
        }

        private FastLineRenderer lotsOfLinesRenderer;

        /// <summary>
        /// Generates a lot of lines
        /// </summary>
        public void GenerateLotsOfLines()
        {
            const int iterations = 10;
            const int batchSize = 16384;

            for (int i = 0; i < iterations; i++)
            {
                if (UseUnityLineRendererToggle != null && UseUnityLineRendererToggle.isOn && UnityLineRenderer != null)
                {
                    for (int j = 0; j < batchSize; j++)
                    {
                        Vector3 pos = new Vector3(UnityEngine.Random.Range(0.0f, Screen.width), UnityEngine.Random.Range(0.0f, Screen.height), UnityEngine.Random.Range(-400.0f, 400.0f));
                        UnityLineRendererPositions.Add(pos);
                    }
                    UnityLineRenderer.positionCount = UnityLineRendererPositions.Count;
                    UnityLineRenderer.SetPositions(UnityLineRendererPositions.ToArray());
                }
                else if (lotsOfLinesRenderer != null)
                {
                    // fast clone, just re-use the mesh!
                    const float range = 1000.0f;
                    FastLineRenderer r = FastLineRenderer.CreateWithParent(null, LineRenderer);
                    r.Material.EnableKeyword("DISABLE_CAPS");
                    r.Mesh = lotsOfLinesRenderer.Mesh;
                    r.transform.Translate(UnityEngine.Random.Range(-range, range), UnityEngine.Random.Range(-range, range), UnityEngine.Random.Range(-range, range));
                }
                else
                {
                    FastLineRenderer r = FastLineRenderer.CreateWithParent(null, LineRenderer);
                    r.Material.EnableKeyword("DISABLE_CAPS");
                    r.SetCapacity(FastLineRenderer.MaxLinesPerMesh * FastLineRenderer.VerticesPerLine);
                    FastLineRendererProperties props = new FastLineRendererProperties();
                    for (int j = 0; j < batchSize; j++)
                    {
                        Color32 randColor = new Color32(RandomByte(), RandomByte(), RandomByte(), RandomByte());
                        Vector3 pos = new Vector3(UnityEngine.Random.Range(0.0f, Screen.width), UnityEngine.Random.Range(0.0f, Screen.height), UnityEngine.Random.Range(-400.0f, 400.0f));
                        props.Start = pos;
                        props.Color = randColor;
                        props.Radius = UnityEngine.Random.Range(1.0f, 4.0f);
                        props.SetLifeTime(float.MaxValue);
                        props.AngularVelocity = UnityEngine.Random.Range(-6.0f, 6.0f);
                        props.Velocity = RandomVelocity(50.0f);
                        r.AppendLine(props);
                    }
                    r.Apply();
                    lotsOfLinesRenderer = r;
                }

                LineCountLabel.text = "Lines: " + (lineCount += (iterations * batchSize));
            }
        }

        /// <summary>
        /// Handle unity vs fast line renderer toggle
        /// </summary>
        public void UseUnityLineRendererToggled()
        {
            FastLineRenderer.ResetAll();
            UnityLineRendererPositions.Clear();
            UnityLineRenderer.positionCount = 0;
            LineCountLabel.text = string.Empty;
            lineCount = 0;
        }

        /// <summary>
        /// Clamp angle
        /// </summary>
        /// <param name="angle">Angle</param>
        /// <param name="min">Min</param>
        /// <param name="max">Max</param>
        /// <returns>Clamped angle</returns>
        public static float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360F)
            {
                angle += 360F;
            }
            if (angle > 360F)
            {
                angle -= 360F;
            }

            return Mathf.Clamp(angle, min, max);
        }
    }
}