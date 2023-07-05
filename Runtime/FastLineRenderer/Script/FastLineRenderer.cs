//
// (c) 2016 Digital Ruby, LLC
// http://www.digitalruby.com
// Code may not be redistributed in source form!
// Using this code in commercial games and apps is fine.
//

using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

namespace DigitalRuby.FastLineRenderer
{
    /// <summary>
    /// Represents a group of line
    /// </summary>
    [Serializable]
    public struct LineGroupList
    {
        /// <summary>
        /// Create a default line group
        /// </summary>
        /// <returns>Default line group</returns>
        public static LineGroupList Default()
        {
            LineGroupList l = new LineGroupList();

            l.LineRadius = 2.0f;
            l.LineColor = Color.green;
            l.GlowWidthMultiplier = 0.0f;
            l.GlowIntensity = 0.4f;
            l.AddStartCap = true;
            l.AddEndCap = true;
            l.LineJoin = FastLineRendererLineJoin.Round;
            l.Continuous = true;

            return l;
        }

        /// <summary>
        /// Description. Not saved when built.
        /// </summary>
        [Tooltip("Description. Not saved when built.")]
        public string Description;

        /// <summary>
        /// Offset for all lines in this group
        /// </summary>
        [Tooltip("Offset for all lines in this group")]
        public Vector3 Offset;

        /// <summary>
        /// Line radius
        /// </summary>
        [Range(0.01f, 100.0f)]
        [Tooltip("Line radius")]
        public float LineRadius;

        /// <summary>
        /// Line color
        /// </summary>
        [Tooltip("Line color")]
        public Color32 LineColor;

        /// <summary>
        /// Glow width multiplier
        /// </summary>
        [Range(0.0f, 64.0f)]
        [Tooltip("Glow width multiplier")]
        public float GlowWidthMultiplier;

        /// <summary>
        /// Glow intensity
        /// </summary>
        [Range(0.0f, 4.0f)]
        [Tooltip("Glow intensity")]
        public float GlowIntensity;

        /// <summary>
        /// Whether to add a start cap
        /// </summary>
        [Tooltip("Whether to add a start cap")]
        public bool AddStartCap;

        /// <summary>
        /// Whether to add an end cap
        /// </summary>
        [Tooltip("Whether to add an end cap")]
        public bool AddEndCap;

        /// <summary>
        /// Join type
        /// </summary>
        [Tooltip("Join type")]
        public FastLineRendererLineJoin LineJoin;

        /// <summary>
        /// Continuous. If true, line points append. If false, every two points is an individual line.
        /// </summary>
        [Tooltip("Continuous. If true, line points append. If false, every two points is an individual line.")]
        public bool Continuous;

        /// <summary>
        /// List of points for the lines.
        /// </summary>
        [ReorderableList("List of points for the lines.")]
        public ReorderableList_Vector3 Points;
    }

    /// <summary>
    /// Line join modes
    /// </summary>
    public enum FastLineRendererLineJoin
    {
        /// <summary>
        /// No attempt to join
        /// </summary>
        None,

        /// <summary>
        /// Adjust the position of the line to intersect the previous line
        /// </summary>
        AdjustPosition,

        /// <summary>
        /// Force the vertices of the line to attach to the previous line. This looks worse at sharper angles, and AdjustPosition should be used for those cases.
        /// </summary>
        AttachToPrevious,

        /// <summary>
        /// Round line join
        /// </summary>
        Round
    }

    /// <summary>
    /// Type of line segment
    /// </summary>
    public enum FastLineRendererLineSegmentType
    {
        /// <summary>
        /// None
        /// </summary>
        None = 0,

        /// <summary>
        /// Full line segment
        /// </summary>
        Full = 2,

        /// <summary>
        /// Start cap
        /// </summary>
        StartCap = 4,

        /// <summary>
        /// End cap
        /// </summary>
        EndCap = 8,

        /// <summary>
        /// Round join
        /// </summary>
        RoundJoin = 16
    }

    /// <summary>
    /// Spline flags
    /// </summary>
    public enum FastLineRendererSplineFlags
    {
        /// <summary>
        /// No flags
        /// </summary>
        None = 0,

        /// <summary>
        /// Loop back to start (close path)
        /// </summary>
        ClosePath = 1,

        /// <summary>
        /// Add a start cap
        /// </summary>
        StartCap = 2,

        /// <summary>
        /// Add an end cap
        /// </summary>
        EndCap = 4
    }

    /// <summary>
    /// Properties for creating lines
    /// </summary>
    public class FastLineRendererProperties
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public FastLineRendererProperties()
        {
            LifeTime.x = Time.timeSinceLevelLoad;
        }

        /// <summary>
        /// Infinite lifetime and no fading. Line exists until manually destroyed.
        /// </summary>
        public static Vector4 LifeTimeInfinite()
        {
            return new Vector4(Time.timeSinceLevelLoad, 0.0f, 999999999.0f, 0.0f);
        }

        /// <summary>
        /// Add n seconds to the creation time of these properties. Useful for animation when you want segments to animate in.
        /// </summary>
        /// <param name="seconds">Seconds to add to the creation time</param>
        public void AddCreationTimeSeconds(float seconds)
        {
            LifeTime.x += seconds;
        }

        /// <summary>
        /// Clone these properties
        /// </summary>
        /// <returns>Cloned FastLineRendererProperties</returns>
        public FastLineRendererProperties Clone()
        {
            return new FastLineRendererProperties
            {
                Start = Start,
                End = End,
                Radius = Radius,
                Color = Color,
                GlowWidthMultiplier = GlowWidthMultiplier,
                GlowIntensityMultiplier = GlowIntensityMultiplier,
                LifeTime = LifeTime,
                Velocity = Velocity,
                AngularVelocity = AngularVelocity,
                LineJoin = LineJoin,
                LineType = LineType
            };
        }

        /// <summary>
        /// Start position
        /// </summary>
        public Vector3 Start;

        /// <summary>
        /// End position (ignored for AppendLine, in which case Start is used)
        /// </summary>
        public Vector3 End;

        /// <summary>
        /// Line radius in world units
        /// </summary>
        public float Radius = 4.0f;

        /// <summary>
        /// Color
        /// </summary>
        public Color32 Color = UnityEngine.Color.white;

        /// <summary>
        /// Glow width multiplier
        /// </summary>
        public float GlowWidthMultiplier = 4.0f;

        /// <summary>
        /// Glow intensity multiplier
        /// </summary>
        public float GlowIntensityMultiplier = 0.5f;

        /// <summary>
        /// Life time parameters. Do not modify, instead call SetLifeTime.
        /// </summary>
        public Vector4 LifeTime = LifeTimeInfinite();

        /// <summary>
        /// Velocity of the line.
        /// </summary>
        public Vector3 Velocity;

        /// <summary>
        /// Angular velocity
        /// </summary>
        public float AngularVelocity { get { return LifeTime.w; } set { LifeTime.w = value; } }

        /// <summary>
        /// Join mode if AppendLine is used
        /// </summary>
        public FastLineRendererLineJoin LineJoin = FastLineRendererLineJoin.AdjustPosition;

        /// <summary>
        /// Line type
        /// </summary>
        public FastLineRendererLineSegmentType LineType = FastLineRendererLineSegmentType.Full;

        /// <summary>
        /// Populates the LifeTime vector
        /// </summary>
        /// <param name="creationTime">Time at which the line is created (where now is Time.timeSinceLevelLoad)</param>
        /// <param name="lifeTime">Total lifetime for the line.</param>
        /// <param name="fadeSeconds">Seconds to fade in and out</param>
        public void SetLifeTime(float creationTime, float lifeTime, float fadeSeconds)
        {
            LifeTime.x = creationTime;
            LifeTime.y = fadeSeconds;
            LifeTime.z = lifeTime;
        }

        /// <summary>
        /// Populates the LifeTime vector
        /// </summary>
        /// <param name="lifeTime">Total lifetime for the line.</param>
        /// <param name="fadeSeconds">Seconds to fade in and out.</param>
        public void SetLifeTime(float lifeTime, float fadeSeconds)
        {
            LifeTime.x = Time.timeSinceLevelLoad;
            LifeTime.y = fadeSeconds;
            LifeTime.z = lifeTime;
        }

        /// <summary>
        /// Populates the LifeTime vector with no fade
        /// </summary>
        /// <param name="lifeTime">Total lifetime of the line.</param>
        public void SetLifeTime(float lifeTime)
        {
            LifeTime.x = Time.timeSinceLevelLoad;
            LifeTime.y = 0.0f;
            LifeTime.z = lifeTime;
        }
    }

    /// <summary>
    /// Fast line renderer script
    /// </summary>
    [ExecuteInEditMode]
    public class FastLineRenderer : MonoBehaviour
    {
        /// <summary>
        /// Maximum vertices per mesh
        /// </summary>
        public const int MaxVerticesPerMesh = 65000;

        /// <summary>
        /// Maximum number of lines (quads) per mesh
        /// </summary>
        public const int MaxLinesPerMesh = MaxVerticesPerMesh / 4;

        /// <summary>
        /// Maximum indices per mesh
        /// </summary>
        public const int MaxIndicesPerMesh = (int)((float)MaxVerticesPerMesh * 1.5);

        /// <summary>
        /// Number of vertices per line / quad
        /// </summary>
        public const int VerticesPerLine = 4;

        /// <summary>
        /// Contains indices that allow rendering quads with a mesh, using QuadUV* uv coordinates. Array is MaxIndicesPerMesh in size, allowing
        /// you to pull out that exact amount of indices you need into a new array. Unity really needs to provide index and count parameters
        /// to really optimize the use of this array.
        /// </summary>
        public static readonly int[] QuadIndices = new int[MaxIndicesPerMesh];

        /// <summary>
        /// Quad UV1
        /// </summary>
        public static readonly Vector2 QuadUV1 = new Vector2(0.0f, 0.0f);

        /// <summary>
        /// Quad UV2
        /// </summary>
        public static readonly Vector2 QuadUV2 = new Vector2(1.0f, 0.0f);

        /// <summary>
        /// Quad UV3
        /// </summary>
        public static readonly Vector2 QuadUV3 = new Vector2(0.0f, 1.0f);

        /// <summary>
        /// Quad UV4
        /// </summary>
        public static readonly Vector2 QuadUV4 = new Vector2(1.0f, 1.0f);

        private static int mainTexId;
        private static int mainTexStartCapId;
        private static int mainTexEndCapId;
        private static int mainTexRoundJoinId;
        private static int animationSpeedId;
        private static int uvxScaleId;
        private static int uvyScaleId;
        private static int tintColorId;
        private static int glowIntensityMultiplierId;
        private static int glowWidthMultiplierId;
        private static int glowLengthMultiplierId;
        private static int jitterMultiplierId;
        private static int turbulenceMultiplierId;
        private static int screenRadiusMultiplierId;
        private static int timeBaseId;

        private const int defaultListCapacity = 256;
        private static readonly LineGroupList[] defaultInitialGroups = new LineGroupList[] { LineGroupList.Default() };
        private static readonly HashSet<FastLineRenderer> currentLineRenderers = new HashSet<FastLineRenderer>();
        private static readonly HashSet<FastLineRenderer> cache = new HashSet<FastLineRenderer>();

        private readonly List<Mesh> meshes = new List<Mesh>();
        private readonly List<MeshRenderer> meshRenderersGlow = new List<MeshRenderer>();
        private readonly List<MeshRenderer> meshRenderersNoGlow = new List<MeshRenderer>();
        private readonly List<List<Vector4>> texCoordsAndGlowLists = new List<List<Vector4>>(new[] { new List<Vector4>(defaultListCapacity) });
        private readonly List<List<Vector3>> verticesLists = new List<List<Vector3>>(new[] { new List<Vector3>(defaultListCapacity) });
        private readonly List<List<Vector4>> lineDirsLists = new List<List<Vector4>>(new[] { new List<Vector4>(defaultListCapacity) });
        private readonly List<List<Color32>> colorsLists = new List<List<Color32>>(new[] { new List<Color32>(defaultListCapacity) });
        private readonly List<List<Vector3>> endsLists = new List<List<Vector3>>(new[] { new List<Vector3>(defaultListCapacity) });
        private readonly List<List<Vector4>> lifeTimesLists = new List<List<Vector4>>(new[] { new List<Vector4>(defaultListCapacity) });
        private readonly List<Bounds> boundsList = new List<Bounds>(new[] { new Bounds() });
        private readonly List<Vector3> path = new List<Vector3>();

        private Vector3? lastPoint;
        private int listIndex;
        private List<Vector4> texCoordsAndGlow;
        private List<Vector3> vertices;
        private List<Vector4> lineDirs;
        private List<Color32> colors;
        private List<Vector3> velocities;
        private List<Vector4> lifeTimes;

        private const int boundsPadder = 1000000000;
        private int currentBoundsMinX = int.MaxValue - boundsPadder;
        private int currentBoundsMinY = int.MaxValue - boundsPadder;
        private int currentBoundsMinZ = int.MaxValue - boundsPadder;
        private int currentBoundsMaxX = int.MinValue + boundsPadder;
        private int currentBoundsMaxY = int.MinValue + boundsPadder;
        private int currentBoundsMaxZ = int.MinValue + boundsPadder;

        private CanvasRenderer canvasRenderer;
        private bool useCanvas;

        /// <summary>
        /// Material to render the lines with (glow)
        /// </summary>
        [Header("Material")]
        [Tooltip("Material to render the lines with (glow)")]
        public Material Material;
        private Material previousMaterial;
        private MaterialPropertyBlock materialPropertyBlock;

        /// <summary>
        /// Material to render the lines with (no glow)
        /// </summary>
        [Tooltip("Material to render the lines with (no glow)")]
        public Material MaterialNoGlow;
        private Material previousMaterialNoGlow;
        private MaterialPropertyBlock materialNoGlowPropertyBlock;

        /// <summary>
        /// Camera. Defaults to main camera.
        /// </summary>
        [Header("Camera")]
        [Tooltip("Camera. Defaults to main camera.")]
        public Camera Camera;

        /// <summary>
        /// True to use world space, false to use local space. Default is local space. World space will orient at 0,0,0 in world space. Local space will orient at 0,0,0 relative to parent.
        /// </summary>
        [Tooltip("True to use world space, false to use local space. Default is local space. World space will orient at 0,0,0 in world space. Local space will orient at 0,0,0 relative to parent.")]
        public bool UseWorldSpace;

        /// <summary>
        /// Animation speed. Used for marching ants style animation.
        /// </summary>
        [Header("Animation")]
        [Range(-100.0f, 100.0f)]
        [Tooltip("Animation speed. Used for marching ants style animation.")]
        public float AnimationSpeed;

        /// <summary>
        /// Jitter multiplier. Applies globally in the Material rather than per vertex.
        /// </summary>
        [Tooltip("Jitter multiplier. Applies globally in the Material rather than per vertex.")]
        [Range(0.0f, 100.0f)]
        public float JitterMultiplier;

        /// <summary>
        /// Turbulence. Requires lines to have LifeTime setup and works only at runtime. Applies globally in the Material rather than per vertex.
        /// </summary>
        [Tooltip("Turbulence. Requires lines to have LifeTime setup and works only at runtime. Applies globally in the Material rather than per vertex.")]
        [Range(0.0f, 100.0f)]
        public float Turbulence;

        /// <summary>
        /// Line UV X Scale. If not 1, Ensure your material texture is set to repeat. Applies globally in the Material rather than per vertex.
        /// </summary>
        [Header("Scale")]
        [Range(0.0f, 256.0f)]
        [Tooltip("Line UV X Scale. If not 1, Ensure your material texture is set to repeat. Applies globally in the Material rather than per vertex.")]
        public float LineUVXScale = 1.0f;

        /// <summary>
        /// Line UV Y Scale. If not 1, Ensure your material texture is set to repeat. Applies globally in the Material rather than per vertex.
        /// </summary>
        [Range(0.0f, 256.0f)]
        [Tooltip("Line UV Y Scale. If not 1, Ensure your material texture is set to repeat. Applies globally in the Material rather than per vertex.")]
        public float LineUVYScale = 1.0f;

        /// <summary>
        /// Glow animation speed. Used for marching ants style animation.
        /// </summary>
        [Range(-100.0f, 100.0f)]
        [Tooltip("Glow animation speed. Used for marching ants style animation.")]
        public float GlowAnimationSpeed;

        /// <summary>
        /// Line glow UV X Scale. If not 1, Ensure your material glow texture is set to repeat. Applies globally in the Material rather than per vertex.
        /// </summary>
        [Range(0.0f, 256.0f)]
        [Tooltip("Line glow UV X Scale. If not 1, Ensure your material glow texture is set to repeat. Applies globally in the Material rather than per vertex.")]
        public float GlowUVXScale = 1.0f;

        /// <summary>
        /// Line glow UV Y Scale. IF not 1, Ensure your material glow texture is set to repeat. Applies globally in the Material rather than per vertex.
        /// </summary>
        [Range(0.0f, 256.0f)]
        [Tooltip("Line glow UV Y Scale. IF not 1, Ensure your material glow texture is set to repeat. Applies globally in the Material rather than per vertex.")]
        public float GlowUVYScale = 1.0f;

        /// <summary>
        /// Scale for start caps.
        /// </summary>
        [Range(0.0f, 10.0f)]
        [Tooltip("Scale for start caps.")]
        public float StartCapScale = 1.0f;

        /// <summary>
        /// Scale for end caps.
        /// </summary>
        [Range(0.0f, 10.0f)]
        [Tooltip("Scale for end caps.")]
        public float EndCapScale = 1.0f;

        /// <summary>
        /// Tint color. Applies globally to all lines and is applied in addition to individual line colors.
        /// </summary>
        [Header("Colors and Glow")]
        [Tooltip("Tint color. Applies globally to all lines and is applied in addition to individual line colors.")]
        public Color32 TintColor = Color.white;

        /// <summary>
        /// Glow color. Applies globally in the Material rather than per vertex.
        /// </summary>
        [Tooltip("Glow color. Applies globally in the Material rather than per vertex.")]
        public Color32 GlowColor = Color.blue;

        /// <summary>
        /// Glow itensity multiplier. Applies globally in the Material rather than per vertex. Set to 0 for no glow.
        /// </summary>
        [Range(0.0f, 3.0f)]
        [Tooltip("Glow itensity multiplier. Applies globally in the Material rather than per vertex. Set to 0 for no glow.")]
        public float GlowIntensityMultiplier = 1.0f;

        /// <summary>
        /// Glow width multiplier. Applies globally in the Material rather than per vertex.
        /// </summary>
        [Range(0.0f, 16.0f)]
        [Tooltip("Glow width multiplier. Applies globally in the Material rather than per vertex.")]
        public float GlowWidthMultiplier = 1.0f;

        /// <summary>
        /// Glow length multiplier. Applies globally in the Material rather than per vertex.
        /// </summary>
        [Range(0.0f, 2.0f)]
        [Tooltip("Glow length multiplier. Applies globally in the Material rather than per vertex.")]
        public float GlowLengthMultiplier = 0.4f;

        /// <summary>
        /// Line texture
        /// </summary>
        [Header("Textures")]
        [Tooltip("Line texture")]
        public Texture2D LineTexture;

        /// <summary>
        /// Line texture - start cap
        /// </summary>
        [Tooltip("Line texture - start cap")]
        public Texture2D LineTextureStartCap;

        /// <summary>
        /// Line texture - end cap
        /// </summary>
        [Tooltip("Line texture - end cap")]
        public Texture2D LineTextureEndCap;

        /// <summary>
        /// Line texture - round join
        /// </summary>
        [Tooltip("Line texture - round join")]
        public Texture2D LineTextureRoundJoin;

        /// <summary>
        /// Glow texture
        /// </summary>
        [Tooltip("Glow texture")]
        public Texture2D GlowTexture;

        /// <summary>
        /// Glow texture - start cap
        /// </summary>
        [Tooltip("Glow texture - start cap")]
        public Texture2D GlowTextureStartCap;

        /// <summary>
        /// Glow texture - end cap
        /// </summary>
        [Tooltip("Glow texture - end cap")]
        public Texture2D GlowTextureEndCap;

        /// <summary>
        /// Glow texture - round join
        /// </summary>
        [Tooltip("Glow texture - round join")]
        public Texture2D GlowTextureRoundJoin;

        /// <summary>
        /// Amount to scale the mesh by. If you aren't using GPU properties that modify position (i.e. turbulence, velocity and angular velocity) you can leave as 1.
        /// If you are using GPU properties, you will want to assign a value that is large enough to scale the mesh size so that vertices are visible for the lifetime of the lines.
        /// </summary>
        [Header("Other")]
        [Tooltip("Amount to scale the mesh by. If you aren't using GPU properties that modify position (i.e. turbulence, velocity and angular velocity) you can leave as 1. " +
            "If you are using GPU properties, you will want to assign a value that is large enough to scale the mesh size so that vertices are visible for the lifetime of the lines.")]
        public Vector3 BoundsScale = Vector3.one;

        /// <summary>
        /// Experimental, screen radius multiplier, attempts to make lines stay the same radius on screen using this multiplier
        /// </summary>
        [Tooltip("Experimental, screen radius multiplier, attempts to make lines stay the same radius on screen using this multiplier")]
        public float ScreenRadiusMultiplier;

        /// <summary>
        /// Sort layer
        /// </summary>
        // editable via FastLineRendererEditor
        [HideInInspector]
        [Tooltip("Sort layer")]
        public string SortLayerName;

        /// <summary>
        /// Sort order in layer
        /// </summary>
        // editable via FastLineRendererEditor
        [HideInInspector]
        [Tooltip("Order in sort layer")]
        public int SortOrderInLayer;

        /// <summary>
        /// Initial set of lines. Leave empty if you are generating your lines in script.
        /// </summary>
        [Tooltip("Initial set of lines. Leave empty if you are generating your lines in script.")]
        public LineGroupList[] InitialLineGroups;

        /// <summary>
        /// Get or set the mesh that this fast line renderer is using, or null if no mesh is being used yet. If getting a mesh, you must call Apply first, which will create the mesh.
        /// Setting the mesh will clear the fast line renderer first.
        /// </summary>
        public Mesh Mesh
        {
            get
            {
                return meshes.Count == 0 ? null : meshes[0];
            }
            set
            {
                if (value != null)
                {
                    Reset();
                    meshes.Add(value);
                    CreateMeshObject(value);
                }
            }
        }

        /// <summary>
        /// Static constructor
        /// </summary>
        static FastLineRenderer()
        {
            int vertexIndex = 0;
            int index = 0;
            while (vertexIndex != MaxVerticesPerMesh)
            {
                QuadIndices[index++] = vertexIndex++;
                QuadIndices[index++] = vertexIndex++;
                QuadIndices[index++] = vertexIndex;
                QuadIndices[index++] = vertexIndex--;
                QuadIndices[index++] = vertexIndex;
                QuadIndices[index++] = (vertexIndex += 2);
                vertexIndex++;
            }
        }

        private void CreateNewSetOfLists()
        {
            texCoordsAndGlow = new List<Vector4>(defaultListCapacity);
            vertices = new List<Vector3>(defaultListCapacity);
            lineDirs = new List<Vector4>(defaultListCapacity);
            colors = new List<Color32>(defaultListCapacity);
            velocities = new List<Vector3>(defaultListCapacity);
            lifeTimes = new List<Vector4>(defaultListCapacity);

            texCoordsAndGlowLists.Add(texCoordsAndGlow);
            verticesLists.Add(vertices);
            lineDirsLists.Add(lineDirs);
            colorsLists.Add(colors);
            endsLists.Add(velocities);
            lifeTimesLists.Add(lifeTimes);

            listIndex = verticesLists.Count - 1;
        }

        private void AssignLists()
        {
            texCoordsAndGlow = texCoordsAndGlowLists[listIndex];
            vertices = verticesLists[listIndex];
            lineDirs = lineDirsLists[listIndex];
            colors = colorsLists[listIndex];
            velocities = endsLists[listIndex];
            lifeTimes = lifeTimesLists[listIndex];

            currentBoundsMinX = (int)boundsList[listIndex].min.x;
            currentBoundsMinY = (int)boundsList[listIndex].min.y;
            currentBoundsMinZ = (int)boundsList[listIndex].min.z;
            currentBoundsMaxX = (int)boundsList[listIndex].max.x;
            currentBoundsMaxY = (int)boundsList[listIndex].max.y;
            currentBoundsMaxZ = (int)boundsList[listIndex].max.z;
        }

        private void UpdateCurrentLists()
        {
            if (vertices.Count == MaxVerticesPerMesh)
            {
                UpdateCurrentBounds();
                ResetCurrentBounds();

                if (listIndex >= verticesLists.Count - 1)
                {
                    CreateNewSetOfLists();
                }
                else
                {
                    listIndex++;
                    AssignLists();
                }
            }
        }

        private void ResetCurrentBounds()
        {
            currentBoundsMinX = currentBoundsMinY = currentBoundsMinZ = int.MaxValue - boundsPadder;
            currentBoundsMaxX = currentBoundsMaxY = currentBoundsMaxZ = int.MinValue + boundsPadder;
        }

        private void Cleanup(bool destroy)
        {
            foreach (Mesh mesh in meshes)
            {
                if (mesh != null)
                {
                    if (destroy)
                    {
                        DestroyImmediate(mesh, true);
                    }
                    else
                    {
                        mesh.Clear();
                    }                    
                }
            }

            if (destroy)
            {
                foreach (MeshRenderer meshRenderer in meshRenderersGlow)
                {
                    if (meshRenderer != null)
                    {
                        DestroyImmediate(meshRenderer, true);
                    }
                }
                foreach (MeshRenderer meshRenderer in meshRenderersNoGlow)
                {
                    if (meshRenderer != null)
                    {
                        DestroyImmediate(meshRenderer, true);
                    }
                }
            }

            foreach (var list in verticesLists)
            {
                list.Clear();
            }
            foreach (var list in texCoordsAndGlowLists)
            {
                list.Clear();
            }
            foreach (var list in lineDirsLists)
            {
                list.Clear();
            }
            foreach (var list in colorsLists)
            {
                list.Clear();
            }
            foreach (var list in endsLists)
            {
                list.Clear();
            }
            foreach (var list in lifeTimesLists)
            {
                list.Clear();
            }
            for (int i = 0; i < boundsList.Count; i++)
            {
                boundsList[i] = new Bounds();
            }
            ResetCurrentBounds();

            if (destroy)
            {
                for (int i = 0; i < gameObject.transform.childCount; i++)
                {
                    GameObject obj = gameObject.transform.GetChild(i).gameObject;
                    if (obj != null && obj.name.StartsWith("FastLineRenderer"))
                    {
                        DestroyImmediate(obj, true);
                    }
                }
                meshes.Clear();
                meshRenderersGlow.Clear();
                meshRenderersNoGlow.Clear();
            }

            path.Clear();

            listIndex = 0;
            lastPoint = null;

            AssignLists();
        }

        private void UpdateCurrentBounds()
        {
            Bounds b = new Bounds();
            Vector3 min = new Vector3(currentBoundsMinX - 2, currentBoundsMinY - 2, currentBoundsMinZ - 2);
            Vector3 max = new Vector3(currentBoundsMaxX + 2, currentBoundsMaxY + 2, currentBoundsMaxZ + 2);
            Vector3 size = (max - min);
            size.x *= BoundsScale.x;
            size.y *= BoundsScale.y;
            size.z *= BoundsScale.z;
            b.center = (max + min) * 0.5f;
            b.size = size;
            if (listIndex >= boundsList.Count)
            {
                boundsList.Add(b);
            }
            else
            {
                boundsList[listIndex] = b;
            }
        }

        private void ApplyListsToMeshes()
        {
            UpdateCurrentBounds();

            for (int index = 0; index <= listIndex; index++)
            {
                EnsureMeshCount(index);
                Mesh mesh = meshes[index];
                List<Vector3> vertices = verticesLists[index];
                mesh.SetVertices(vertices);
                mesh.SetTangents(lineDirsLists[index]);
                mesh.SetColors(colorsLists[index]);
                mesh.SetNormals(endsLists[index]);

                if (canvasRenderer == null && !useCanvas)
                {
                    mesh.SetUVs(0, texCoordsAndGlowLists[index]);
                    mesh.SetUVs(1, lifeTimesLists[index]);
                }
                else
                {
                    // canvas renderer mesh can only use x and y for UV - I've submitted a bug to Unity
                    List<Vector4> texCoordsAndGlow = texCoordsAndGlowLists[index];
                    List<Vector4> lifeTimes = lifeTimesLists[index];
                    Vector2[] texcoord0 = new Vector2[texCoordsAndGlow.Count];
                    Vector2[] texcoord1 = new Vector2[texCoordsAndGlow.Count];
                    Vector2[] texcoord2 = new Vector2[texCoordsAndGlow.Count];
                    Vector2[] texcoord3 = new Vector2[texCoordsAndGlow.Count];
                    for (int i = 0; i < texCoordsAndGlow.Count; i++)
                    {
                        texcoord0[i] = new Vector2(texCoordsAndGlow[i].x, texCoordsAndGlow[i].y);
                        texcoord1[i] = new Vector2(texCoordsAndGlow[i].z, texCoordsAndGlow[i].w);
                        texcoord2[i] = new Vector2(lifeTimes[i].x, lifeTimes[i].y);
                        texcoord3[i] = new Vector2(lifeTimes[i].z, lifeTimes[i].w);
                    }
                    mesh.uv = texcoord0;
                    mesh.uv2 = texcoord1;
                    mesh.uv3 = texcoord2;
                    mesh.uv4 = texcoord3;
                }

                int indicesCount = (int)(vertices.Count * 1.5f);
                if (indicesCount == MaxIndicesPerMesh)
                {
                    mesh.triangles = QuadIndices;
                }
                else
                {
                    int[] indicesArray = new int[indicesCount];
                    Array.Copy(QuadIndices, 0, indicesArray, 0, indicesCount);
                    mesh.triangles = indicesArray;
                }
                mesh.bounds = boundsList[index];
                MeshRenderer glowRenderer = meshRenderersGlow[index];
                MeshRenderer noGlowRenderer = meshRenderersNoGlow[index];
                //r.enabled = r2.enabled = true;
                glowRenderer.enabled = (GlowIntensityMultiplier > 0.0f);
                noGlowRenderer.enabled = true;
                glowRenderer.sharedMaterial = Material;
                noGlowRenderer.sharedMaterial = MaterialNoGlow;
                glowRenderer.sortingLayerName = noGlowRenderer.sortingLayerName = SortLayerName;
                glowRenderer.sortingOrder = noGlowRenderer.sortingOrder = SortOrderInLayer;
            }
        }

        private void UpdateCanvasRendererMesh()
        {
            if (canvasRenderer == null)
            {
                return;
            }
            else if (meshes.Count == 0)
            {
                canvasRenderer.SetMesh(null);
            }
            else
            {
                canvasRenderer.SetMesh(meshes[0]);
            }
        }

        private void CreateInitialLines()
        {
            if (InitialLineGroups == null || InitialLineGroups.Length == 0)
            {
                return;
            }

            bool changes = false;
            FastLineRendererProperties props = new FastLineRendererProperties();
            foreach (LineGroupList list in InitialLineGroups)
            {
                if (list.Points == null || list.Points == null || list.Points.List == null || list.Points.List.Count == 0)
                {
                    continue;
                }

                bool first = true;
                Vector3 offset = list.Offset;
                int lastIndex = list.Points.List.Count - 1;
                props.LineJoin = list.LineJoin;

                for (int i = 1; i < list.Points.List.Count; i++)
                {
                    changes = true;
                    Vector3 nextPoint = list.Points.List[i];
                    props.Radius = list.LineRadius;
                    props.Color = list.LineColor;
                    props.GlowWidthMultiplier = list.GlowWidthMultiplier;
                    props.GlowIntensityMultiplier = list.GlowIntensity;

                    if (first)
                    {
                        first = false;
                        props.Start = list.Points.List[i - 1] + offset;
                        props.End = nextPoint + offset;
                        if (list.Continuous)
                        {
                            if (list.AddStartCap)
                            {
                                props.LineType = FastLineRendererLineSegmentType.StartCap;
                                StartLine(props);
                            }
                            else
                            {
                                AddLine(props);
                            }
                        }
                        else
                        {
                            AddLine(props, list.AddStartCap, list.AddEndCap);
                            first = true;
                            i++;
                        }
                    }
                    else
                    {
                        props.Start = nextPoint + offset;
                        if (list.AddEndCap && i == lastIndex)
                        {
                            props.LineType = FastLineRendererLineSegmentType.EndCap;
                            EndLine(props);
                        }
                        else
                        {
                            AppendLine(props);
                        }
                    }
                }
            }
            if (changes)
            {
                Apply();
            }
        }

        private void CheckInitialLines()
        {

#if UNITY_EDITOR

            // detect increase in array size and reset last item to default instead of Unity's stupid cloning of last item
            if (!Application.isPlaying)
            {
                Cleanup(false);
                CreateInitialLines();
            }
            else

#endif

            {
                if (InitialLineGroups != null && InitialLineGroups.Length != 0)
                {
                    CreateInitialLines();
                    InitialLineGroups = null;
                }
            }
        }

        private void UpdateCamera()
        {
            if (Camera == null)
            {
                Camera = Camera.main;
                if (Camera == null)
                {
                    Camera = Camera.current;
                }
            }
        }

        private void UpdateMaterial(Material m, IEnumerable<MeshRenderer> meshRenderers, MaterialPropertyBlock block,
            Texture2D lineTexture, Texture2D startCap, Texture2D endCap, Texture2D join, Color tintColor, float animationSpeed, Vector2 uvScale)
        {
            if (m == null)
            {
                return;
            }
            else if (Camera == null || !Camera.orthographic)
            {
                m.DisableKeyword("ORTHOGRAPHIC_MODE");
            }
            else
            {
                m.EnableKeyword("ORTHOGRAPHIC_MODE");
            }

            if (canvasRenderer == null && !useCanvas)
            {
                m.DisableKeyword("USE_CANVAS");
            }
            else
            {
                m.EnableKeyword("USE_CANVAS");
                m.SetTexture(mainTexId, lineTexture);
                m.SetTexture(mainTexStartCapId, startCap);
                m.SetTexture(mainTexEndCapId, endCap);
                m.SetTexture(mainTexRoundJoinId, join);
                m.SetFloat(animationSpeedId, animationSpeed);
                m.SetFloat(uvxScaleId, uvScale.x);
                m.SetFloat(uvyScaleId, uvScale.y);
                m.SetColor(tintColorId, tintColor);
                m.SetFloat(glowIntensityMultiplierId, GlowIntensityMultiplier);
                m.SetFloat(glowWidthMultiplierId, GlowWidthMultiplier);
                m.SetFloat(glowLengthMultiplierId, GlowLengthMultiplier);
                m.SetFloat(jitterMultiplierId, JitterMultiplier);
                m.SetFloat(turbulenceMultiplierId, Turbulence);
                m.SetFloat(screenRadiusMultiplierId, ScreenRadiusMultiplier);
                return;
            }

            if (block == null)
            {
                return;
            }

            foreach (MeshRenderer r in meshRenderers)
            {
                if (!r.enabled)
                {
                    continue;
                }

                r.GetPropertyBlock(block);
                if (lineTexture != null)
                {
                    block.SetTexture(mainTexId, lineTexture);
                }
                if (startCap != null)
                {
                    block.SetTexture(mainTexStartCapId, startCap);
                }
                if (endCap != null)
                {
                    block.SetTexture(mainTexEndCapId, endCap);
                }
                if (join != null)
                {
                    block.SetTexture(mainTexRoundJoinId, join);
                }
                block.SetFloat(animationSpeedId, animationSpeed);
                block.SetFloat(uvxScaleId, uvScale.x);
                block.SetFloat(uvyScaleId, uvScale.y);
                block.SetColor(tintColorId, tintColor);
                block.SetFloat(glowIntensityMultiplierId, GlowIntensityMultiplier);
                block.SetFloat(glowWidthMultiplierId, GlowWidthMultiplier);
                block.SetFloat(glowLengthMultiplierId, GlowLengthMultiplier);
                block.SetFloat(jitterMultiplierId, JitterMultiplier);
                block.SetFloat(turbulenceMultiplierId, Turbulence);
                block.SetFloat(screenRadiusMultiplierId, ScreenRadiusMultiplier);
                r.SetPropertyBlock(block);
            }
        }

        private void UpdateMaterial()
        {
            if (Material == null)
            {
                if (previousMaterial != null)
                {
                    previousMaterial = null;
                    if (canvasRenderer != null)
                    {
                        canvasRenderer.SetMaterial(null, 0);
                    }
                }
            }
            if (MaterialNoGlow == null)
            {
                if (previousMaterialNoGlow != null)
                {
                    previousMaterialNoGlow = null;
                    if (canvasRenderer != null)
                    {
                        canvasRenderer.SetMaterial(null, 1);
                    }
                }
            }

            if (Material != previousMaterial)
            {
                previousMaterial = Material;
                foreach (MeshRenderer meshRenderer in meshRenderersGlow)
                {
                    meshRenderer.sharedMaterial = Material;
                }
                if (canvasRenderer != null)
                {
                    canvasRenderer.materialCount = 1;
                    canvasRenderer.SetMaterial(Material, 0);
                }
            }

            if (MaterialNoGlow != previousMaterialNoGlow)
            {
                previousMaterialNoGlow = MaterialNoGlow;
                foreach (MeshRenderer meshRenderer in meshRenderersNoGlow)
                {
                    meshRenderer.sharedMaterial = MaterialNoGlow;
                }
                if (canvasRenderer != null)
                {
                    canvasRenderer.materialCount = 2;
                    canvasRenderer.SetMaterial(MaterialNoGlow, 1);
                }
            }

            UpdateMaterial(MaterialNoGlow, meshRenderersNoGlow, materialPropertyBlock, LineTexture, LineTextureStartCap, LineTextureEndCap, LineTextureRoundJoin, TintColor, AnimationSpeed, new Vector2(LineUVXScale, LineUVYScale));
            UpdateMaterial(Material, meshRenderersGlow, materialNoGlowPropertyBlock, GlowTexture, GlowTextureStartCap, GlowTextureEndCap, GlowTextureRoundJoin, GlowColor, GlowAnimationSpeed, new Vector2(GlowUVXScale, GlowUVYScale));
        }

        private void ResetVariables()
        {
            StartCapScale = EndCapScale = LineUVXScale = LineUVYScale = GlowUVXScale = GlowUVYScale = 1.0f;
            GlowIntensityMultiplier = 0.4f;
            GlowWidthMultiplier = 1.0f;
            GlowLengthMultiplier = 1.0f;
            TintColor = UnityEngine.Color.white;
            GlowColor = UnityEngine.Color.blue;
            BoundsScale = Vector3.one;
            JitterMultiplier = 1.0f;
            Turbulence = 0.0f;
        }

        private void AssignMaterialIds()
        {
            if (mainTexId != 0)
            {
                return;
            }

            mainTexId = Shader.PropertyToID("_MainTex");
            mainTexStartCapId = Shader.PropertyToID("_MainTexStartCap");
            mainTexEndCapId = Shader.PropertyToID("_MainTexEndCap");
            mainTexRoundJoinId = Shader.PropertyToID("_MainTexRoundJoin");
            animationSpeedId = Shader.PropertyToID("_AnimationSpeed");
            uvxScaleId = Shader.PropertyToID("_UVXScale");
            uvyScaleId = Shader.PropertyToID("_UVYScale");
            tintColorId = Shader.PropertyToID("_TintColor");
            glowIntensityMultiplierId = Shader.PropertyToID("_GlowIntensityMultiplier");
            glowWidthMultiplierId = Shader.PropertyToID("_GlowWidthMultiplier");
            glowLengthMultiplierId = Shader.PropertyToID("_GlowLengthMultiplier");
            jitterMultiplierId = Shader.PropertyToID("_JitterMultiplier");
            turbulenceMultiplierId = Shader.PropertyToID("_Turbulence");
            screenRadiusMultiplierId = Shader.PropertyToID("_ScreenRadiusMultiplier");
            timeBaseId = Shader.PropertyToID("_FastLineRendererTimeBase");
        }

        private void Awake()
        {
            if (materialPropertyBlock == null)
            {
                materialPropertyBlock = new MaterialPropertyBlock();
            }
            if (materialNoGlowPropertyBlock == null)
            {
                materialNoGlowPropertyBlock = new MaterialPropertyBlock();
            }

            canvasRenderer = GetComponent<CanvasRenderer>();
            if (canvasRenderer != null)
            {
                useCanvas = true;
                canvasRenderer.materialCount = 1;
            }
            else
            {
                // SM < 3 does not pass texcoord2 or texcoord3 and does not pass zw texcoord values
                // use canvas gives the lines the best they can be with texcoord0 and texcoord1 xy values
                useCanvas = false && SystemInfo.graphicsShaderLevel < 30;
            }
            AssignMaterialIds();
            currentLineRenderers.Add(this);
            ResetCurrentBounds();
            AssignLists();
            CheckInitialLines();

#if UNITY_EDITOR

            FastLineRenderer[] renderers = GetComponents<FastLineRenderer>();
            if (renderers.Length > 1)
            {
                Debug.LogError("Only one fast line renderer script should be attached to a game object. Multiple fast line renderer scripts on one game object is not supported.");
            }

#endif

        }

        private void Update()
        {
            Shader.SetGlobalFloat(timeBaseId, Time.timeSinceLevelLoad);

#if UNITY_EDITOR

            AssignMaterialIds();
            CheckInitialLines();

#endif

            UpdateCamera();
            UpdateMaterial();
        }

        private void LateUpdate()
        {
            // shut off glow if no glow
            bool enable = (GlowIntensityMultiplier >= 0.0f);
            foreach (MeshRenderer renderer in meshRenderersGlow)
            {
                renderer.enabled = enable;
            }
        }

        private void OnDestroy()
        {
            currentLineRenderers.Remove(this);
            Cleanup(true);
            //gameObject.transform.SetParent(null, true);
            cache.Clear();
        }

        private void SetupMeshRenderer(MeshRenderer meshRenderer, Material material)
        {
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = false;
        }

        private GameObject CreateMeshObject(Mesh mesh)
        {
            GameObject obj = new GameObject();
            obj.name = "FastLineRendererMesh";
            obj.hideFlags = HideFlags.HideAndDontSave;
            obj.transform.parent = gameObject.transform;
            if (UseWorldSpace)
            {
                obj.transform.position = Vector3.zero;
            }
            else
            {
                obj.transform.localPosition = Vector3.zero;
            }
            obj.layer = gameObject.layer;
            GameObject glowObject = new GameObject("FastLineRendererMeshGlow");
            glowObject.transform.parent = obj.transform;
            glowObject.layer = obj.layer;
            glowObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            GameObject noGlowObject = new GameObject("FastLineRendererMeshNoGlow");
            noGlowObject.transform.parent = obj.transform;
            noGlowObject.layer = obj.layer;
            noGlowObject.AddComponent<MeshFilter>().sharedMesh = mesh;

#if UNITY_EDITOR

            obj.hideFlags = glowObject.hideFlags = noGlowObject.hideFlags = HideFlags.HideAndDontSave;

#endif

            MeshRenderer meshRenderer = glowObject.AddComponent<MeshRenderer>();
            MeshRenderer meshRendererNoGlow = noGlowObject.AddComponent<MeshRenderer>();
            meshRenderersGlow.Add(meshRenderer);
            meshRenderersNoGlow.Add(meshRendererNoGlow);
            SetupMeshRenderer(meshRenderer, Material);
            SetupMeshRenderer(meshRendererNoGlow, MaterialNoGlow);
            return obj;
        }

        private T[] GetArray<T>(List<T> list, int index, int count)
        {
            T[] array = new T[count];
            list.CopyTo(index, array, 0, count);

            return array;
        }

        private void EnsureMeshCount(int index)
        {
            if (index >= meshes.Count)
            {
                Mesh m = new Mesh() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                CreateMeshObject(m);
                meshes.Add(m);
            }
        }

        private IEnumerator SendToCacheAfterCoRoutine(TimeSpan elapsed)
        {
            yield return new WaitForSeconds((float)elapsed.TotalSeconds);

            SendToCache();
        }

        private void UpdateBounds(ref Vector3 point1, ref Vector3 point2)
        {
            // r = y + ((x - y) & ((x - y) >> (sizeof(int) * CHAR_BIT - 1))); // min(x, y)
            // r = x - ((x - y) & ((x - y) >> (sizeof(int) * CHAR_BIT - 1))); // max(x, y)

            unchecked
            {
                {
                    int xCalculation = (int)point1.x - (int)point2.x;
                    xCalculation &= (xCalculation >> 31);
                    int xMin = (int)point2.x + xCalculation;
                    int xMax = (int)point1.x - xCalculation;

                    xCalculation = currentBoundsMinX - xMin;
                    xCalculation &= (xCalculation >> 31);
                    currentBoundsMinX = xMin + xCalculation;

                    xCalculation = currentBoundsMaxX - xMax;
                    xCalculation &= (xCalculation >> 31);
                    currentBoundsMaxX = currentBoundsMaxX - xCalculation;
                }
                {
                    int yCalculation = (int)point1.y - (int)point2.y;
                    yCalculation &= (yCalculation >> 31);
                    int yMin = (int)point2.y + yCalculation;
                    int yMax = (int)point1.y - yCalculation;

                    yCalculation = currentBoundsMinY - yMin;
                    yCalculation &= (yCalculation >> 31);
                    currentBoundsMinY = yMin + yCalculation;

                    yCalculation = currentBoundsMaxY - yMax;
                    yCalculation &= (yCalculation >> 31);
                    currentBoundsMaxY = currentBoundsMaxY - yCalculation;
                }
                {
                    int zCalculation = (int)point1.z - (int)point2.z;
                    zCalculation &= (zCalculation >> 31);
                    int zMin = (int)point2.z + zCalculation;
                    int zMax = (int)point1.z - zCalculation;

                    zCalculation = currentBoundsMinZ - zMin;
                    zCalculation &= (zCalculation >> 31);
                    currentBoundsMinZ = zMin + zCalculation;

                    zCalculation = currentBoundsMaxZ - zMax;
                    zCalculation &= (zCalculation >> 31);
                    currentBoundsMaxZ = currentBoundsMaxZ - zCalculation;
                }
            }
        }

        private void AddLineInternal(FastLineRendererProperties props, ref Vector4 dirStart, ref Vector4 dirEnd, FastLineRendererLineSegmentType type)
        {
            int lineType = (int)type;

            UpdateCurrentLists();

            Vector4 texCoordAndGlow = new Vector4(((int)QuadUV1.x | lineType), QuadUV1.y, props.GlowWidthMultiplier, props.GlowIntensityMultiplier);
            lastPoint = props.End;

            dirStart.w = props.Radius;
            vertices.Add(props.Start);
            texCoordsAndGlow.Add(texCoordAndGlow);
            lineDirs.Add(dirStart);
            colors.Add(props.Color);
            velocities.Add(props.Velocity);
            lifeTimes.Add(props.LifeTime);

            dirEnd.w = dirStart.w;
            texCoordAndGlow.x = ((int)QuadUV2.x | lineType);
            texCoordAndGlow.y = QuadUV2.y;
            vertices.Add(props.End);
            texCoordsAndGlow.Add(texCoordAndGlow);
            lineDirs.Add(dirEnd);
            colors.Add(props.Color);
            velocities.Add(props.Velocity);
            lifeTimes.Add(props.LifeTime);

            dirStart.w = -props.Radius;
            texCoordAndGlow.x = ((int)QuadUV3.x | lineType);
            texCoordAndGlow.y = QuadUV3.y;
            vertices.Add(props.Start);
            texCoordsAndGlow.Add(texCoordAndGlow);
            lineDirs.Add(dirStart);
            colors.Add(props.Color);
            velocities.Add(props.Velocity);
            lifeTimes.Add(props.LifeTime);

            dirEnd.w = dirStart.w;
            texCoordAndGlow.x = ((int)QuadUV4.x | lineType);
            texCoordAndGlow.y = QuadUV4.y;
            vertices.Add(props.End);
            texCoordsAndGlow.Add(texCoordAndGlow);
            lineDirs.Add(dirEnd);
            colors.Add(props.Color);
            velocities.Add(props.Velocity);
            lifeTimes.Add(props.LifeTime);

            UpdateBounds(ref props.Start, ref props.End);
        }

        private void AppendLineInternal(FastLineRendererProperties props)
        {
            Vector3 prev = lastPoint.Value;
            Vector4 dir = (props.Start - prev);
            props.End = props.Start;

            if (props.LineJoin == FastLineRendererLineJoin.Round)
            {
                // add a line for the join
                Vector3 radius = new Vector3(props.Radius, 0.0f, 0.0f);
                Vector3 end = props.End;
                Vector4 dirRound = new Vector4(props.Radius + props.Radius, 0.0f, 0.0f, 0.0f);
                float glowIntensity = props.GlowIntensityMultiplier;
                props.GlowIntensityMultiplier = 0.0f;
                props.Start = prev - radius;
                props.End = prev + radius;
                AddLineInternal(props, ref dirRound, ref dirRound, FastLineRendererLineSegmentType.RoundJoin);

                // add the regular line
                props.GlowIntensityMultiplier = glowIntensity;
                props.LineJoin = FastLineRendererLineJoin.None;
                props.Start = prev;
                props.End = end;
                AddLineInternal(props, ref dir, ref dir, props.LineType);

                // restore the join
                props.LineJoin = FastLineRendererLineJoin.Round;
            }
            else if (props.LineJoin == FastLineRendererLineJoin.AdjustPosition)
            {
                // move the start position back to approximate a join
                Vector3 offset = new Vector3(dir.x, dir.y, dir.z).normalized * props.Radius;
                prev -= offset;
                props.Start = prev;
                AddLineInternal(props, ref dir, ref dir, props.LineType);
            }
            else if (props.LineJoin == FastLineRendererLineJoin.AttachToPrevious)
            {
                // use the previous end direction for the start direction
                Vector4 prevDir = lineDirs[lineDirs.Count - 1];
                props.Start = prev;
                AddLineInternal(props, ref prevDir, ref dir, props.LineType);
            }
            else
            {
                // no adjustment
                props.Start = prev;
                AddLineInternal(props, ref dir, ref dir, props.LineType);
            }
        }

        private bool AddStartCapLine(FastLineRendererProperties props)
        {
            if (props.LineType == FastLineRendererLineSegmentType.StartCap)
            {
                // make it square
                Vector3 start = props.Start;
                Vector3 end = props.End;
                Vector4 dir = (Vector4)(end - start);
                float radius = props.Radius;
                props.Radius *= StartCapScale;
                float width = props.Radius + props.Radius;
                props.Start = start - (((Vector3)dir).normalized * width);
                props.End = start;
                AddLineInternal(props, ref dir, ref dir, props.LineType);
                props.Radius = radius;
                props.Start = start;
                props.End = end;
                props.LineType = FastLineRendererLineSegmentType.Full;

                return true;
            }

            return false;
        }

        private bool AddEndCapLine(FastLineRendererProperties props)
        {
            if (props.LineType == FastLineRendererLineSegmentType.EndCap)
            {
                FastLineRendererLineJoin prevJoin = props.LineJoin;
                props.LineJoin = FastLineRendererLineJoin.None;

                // make it square
                float radius = props.Radius;
                props.Radius *= EndCapScale;
                Vector4 prevDir = lineDirs[lineDirs.Count - 1];
                Vector3 dirNorm = new Vector3(prevDir.x, prevDir.y, prevDir.z).normalized;
                props.Start = lastPoint.Value + (dirNorm * (props.Radius + props.Radius));
                AppendLineInternal(props);
                props.LineJoin = prevJoin;
                props.LineType = FastLineRendererLineSegmentType.Full;
                props.Radius = radius;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Reset all FastLineRenderer objects in the scene - this is fast and does not rely on the slow Unity Find* methods.
        /// </summary>
        public static void ResetAll()
        {
            foreach (FastLineRenderer r in currentLineRenderers)
            {
                r.Reset();
            }
        }

        /// <summary>
        /// Set the capacity of all internal lists.
        /// </summary>
        /// <param name="capacity">Capacity</param>
        public void SetCapacity(int capacity)
        {
            foreach (var list in verticesLists)
            {
                list.Capacity = capacity;
            }
            foreach (var list in texCoordsAndGlowLists)
            {
                list.Capacity = capacity;
            }
            foreach (var list in lineDirsLists)
            {
                list.Capacity = capacity;
            }
            foreach (var list in colorsLists)
            {
                list.Capacity = capacity;
            }
            foreach (var list in endsLists)
            {
                list.Capacity = capacity;
            }
            foreach (var list in lifeTimesLists)
            {
                list.Capacity = capacity;
            }
        }

        /// <summary>
        /// Determines if lineCount lines can be added without creating a new mesh
        /// </summary>
        /// <param name="lineCount">Line count. Receives the actual available line count before a new mesh will be created.</param>
        /// <returns>True if lines can be added without creating a new mesh, false otherwise</returns>
        public bool CanAddLines(ref int lineCount)
        {
            int currentLines = (vertices.Count % MaxVerticesPerMesh) / VerticesPerLine;
            lineCount = MaxLinesPerMesh - currentLines;

            return (lineCount > 0);
        }

        /// <summary>
        /// Adds an individual line that is not meant to be joined to other lines
        /// </summary>
        /// <param name="props">Creation properties</param>
        /// <param name="startCap">Whether to add a start cap</param>
        /// <param name="endCap">Whether to add an end cap</param>
        public void AddLine(FastLineRendererProperties props, bool startCap = false, bool endCap = false)
        {
            if (startCap)
            {
                props.LineType = FastLineRendererLineSegmentType.StartCap;
                AddStartCapLine(props);
            }

            Vector4 dir = (props.End - props.Start);
            AddLineInternal(props, ref dir, ref dir, FastLineRendererLineSegmentType.Full);

            if (endCap)
            {
                props.LineType = FastLineRendererLineSegmentType.EndCap;
                AddEndCapLine(props);
            }
        }

        /// <summary>
        /// Adds a line that starts at the first point in the list and ends at the last point of the list
        /// </summary>
        /// <param name="props">Creation properties</param>
        /// <param name="points">Points that will make up the line</param>
        /// <param name="segmentCallback">Optional callback for each line segment so you can change the properties per segment if desired</param>
        /// <param name="startCap">Whether to add a start cap</param>
        /// <param name="endCap">Whether to add an end cap</param>
        public void AddLine(FastLineRendererProperties props, IList<Vector3> points, Action<FastLineRendererProperties> segmentCallback, bool startCap = false, bool endCap = false)
        {
            if (points.Count < 2)
            {
                return;
            }
            else
            {
                props.Start = points[0];
                props.End = points[1];
                if (points.Count == 2)
                {
                    if (segmentCallback != null)
                    {
                        segmentCallback(props);
                    }
                    AddLine(props, startCap, endCap);
                    return;
                }
                else
                {
                    if (startCap)
                    {
                        props.LineType = FastLineRendererLineSegmentType.StartCap;
                    }
                    if (segmentCallback != null)
                    {
                        segmentCallback(props);
                    }
                    StartLine(props);
                }
            }
            for (int i = 2; i < points.Count - 1; i++)
            {
                props.Start = points[i];
                if (segmentCallback != null)
                {
                    segmentCallback(props);
                }
                AppendLine(props);
            }
            if (endCap)
            {
                props.LineType = FastLineRendererLineSegmentType.EndCap;
            }
            if (segmentCallback != null)
            {
                segmentCallback(props);
            }
            props.Start = points[points.Count - 1];
            EndLine(props);
        }

        /// <summary>
        /// Add distinct line segments from a list. The list must contain start and end points, repeating for each segment.
        /// </summary>
        /// <param name="props">Creation properties</param>
        /// <param name="points">List of points</param>
        /// <param name="segmentCallback">Optional callback for each line segment so you can change the properties per segment if desired</param>
        /// <param name="startCap">Whether to add start caps</param>
        /// <param name="endCap">Whether to add end caps</param>
        public void AddLines(FastLineRendererProperties props, IList<Vector3> points, Action<FastLineRendererProperties> segmentCallback, bool startCap = false, bool endCap = false)
        {
            for (int i = 0; i < points.Count - 1;)
            {
                if (segmentCallback != null)
                {
                    segmentCallback(props);
                }
                props.Start = points[i++];
                props.End = points[i++];
                AddLine(props, startCap, endCap);
            }
        }

        /// <summary>
        /// Start a line. Start and End must be set on the properties. Set LineType to StartCap to cap.
        /// </summary>
        /// <param name="props">Creation properties</param>
        public void StartLine(FastLineRendererProperties props)
        {
            FastLineRendererLineJoin prevJoin = props.LineJoin;
            props.LineJoin = FastLineRendererLineJoin.None;

            if (AddStartCapLine(props))
            {
                props.LineType = FastLineRendererLineSegmentType.Full;
            }
            Vector4 dir = (props.End - props.Start);
            AddLineInternal(props, ref dir, ref dir, props.LineType);

            props.LineJoin = prevJoin;
        }

        /// <summary>
        /// Append a line - End value of props is ignored, use Start field instead.
        /// </summary>
        /// <param name="props">Creation properties</param>
        /// <returns>True if line was joined to a previous line, false if no previous line available</returns>
        public bool AppendLine(FastLineRendererProperties props)
        {
            // if no previous line, fallback to AddLine
            if (lastPoint == null || vertices.Count == 0)
            {
                if (lastPoint == null)
                {
                    lastPoint = props.Start;
                }
                else
                {
                    props.End = props.Start;
                    props.Start = lastPoint.Value;
                    AddLine(props);
                }
                return false;
            }

            props.LineType |= FastLineRendererLineSegmentType.Full;
            AppendLineInternal(props);

            return true;
        }

        /// <summary>
        /// Ends a line. use Start on props to specify the end point. Set LineType to EndCap to cap.
        /// </summary>
        /// <param name="props">Creation properties</param>
        /// <returns>True if success, false if a line hasn't been started yet</returns>
        public bool EndLine(FastLineRendererProperties props)
        {
            if (lastPoint == null || vertices.Count == 0)
            {
                return false;
            }

            FastLineRendererLineSegmentType type = props.LineType;
            props.LineType = FastLineRendererLineSegmentType.Full;
            AppendLineInternal(props);
            props.LineType = type;

            AddEndCapLine(props);

            lastPoint = null;

            return true;
        }

        /// <summary>
        /// Append a quad/bezier curve to the fast line renderer. The line curves from props.Start to props.End, using the two control points to curve.
        /// </summary>
        /// <param name="props">Line properties</param>
        /// <param name="ctr1">Control point 1</param>
        /// <param name="ctr2">Control point 2</param>
        /// <param name="numberOfSegments">Number of segments. The higher the better quality but more CPU and GPU usage.</param>
        /// <param name="startCap">Whether to add a start cap</param>
        /// <param name="endCap">Whether to add an end cap</param>
        /// <param name="animationTime">The time it takes for each line segment of the spline to animate in</param>
        public void AppendCurve(FastLineRendererProperties props, Vector3 ctr1, Vector3 ctr2, int numberOfSegments, bool startCap, bool endCap, float animationTime = 0.0f)
        {
            float xLifeTime = props.LifeTime.x;
            PathGenerator.Is2D = Camera.orthographic;
            FastLineRendererLineJoin prevJoin = props.LineJoin;
            props.LineJoin = FastLineRendererLineJoin.AttachToPrevious;
            FastLineRendererLineSegmentType prevType = props.LineType;
            props.LineType = FastLineRendererLineSegmentType.Full;
            PathGenerator.CreateCurve(path, props.Start, props.End, ctr1, ctr2, numberOfSegments, 0.0f);
            int index = 2;
            int lastIndexMinusOne = path.Count - 1;
            props.Start = path[0];
            props.End = path[1];
            if (startCap)
            {
                props.LineType = FastLineRendererLineSegmentType.StartCap;
            }
            StartLine(props);
            props.LifeTime.x += animationTime;

            for (; index < path.Count; index++)
            {
                props.Start = path[index];
                if (index == lastIndexMinusOne)
                {
                    if (endCap)
                    {
                        props.LineType = FastLineRendererLineSegmentType.EndCap;
                    }
                    EndLine(props);
                    props.LifeTime.x += animationTime;
                }
                else
                {
                    AppendLine(props);
                    props.LifeTime.x += animationTime;
                }
            }

            path.Clear();
            props.LineJoin = prevJoin;
            props.LineType = prevType;
            props.LifeTime.x = xLifeTime;
        }

        /// <summary>
        /// Append a spline to the fast line renderer. Start and End in props is ignored.
        /// </summary>
        /// <param name="props">Line properties</param>
        /// <param name="points">Points for the spline to follow</param>
        /// <param name="numberOfSegments">Total number of line segments for the spline. The higher this number, the higher quality, but more CPU / GPU time.</param>
        /// <param name="flags">Flags determining how the spline behaves</param>
        /// <param name="animationTime">The time it takes for each line segment of the spline to animate in</param>
        /// <returns>True if success, false if points length is too small</returns>
        public bool AppendSpline(FastLineRendererProperties props, IList<Vector3> points, int numberOfSegments, FastLineRendererSplineFlags flags, float animationTime = 0.0f)
        {
            PathGenerator.Is2D = Camera.orthographic;
            bool closePath = (flags & FastLineRendererSplineFlags.ClosePath) == FastLineRendererSplineFlags.ClosePath;
            bool startCap = (flags & FastLineRendererSplineFlags.StartCap) == FastLineRendererSplineFlags.StartCap;
            bool endCap = (flags & FastLineRendererSplineFlags.EndCap) == FastLineRendererSplineFlags.EndCap;

            if (!PathGenerator.CreateSpline(path, points, numberOfSegments, closePath))
            {
                return false;
            }

            float xLifeTime = props.LifeTime.x;
            int index = 2;
            int lastIndexMinusOne = path.Count - 1;

            FastLineRendererLineJoin prevJoin = props.LineJoin;
            props.LineJoin = FastLineRendererLineJoin.AttachToPrevious;
            FastLineRendererLineSegmentType prevType = props.LineType;
            props.LineType = FastLineRendererLineSegmentType.Full;

            props.Start = path[0];
            props.End = path[1];
            if (startCap)
            {
                props.LineType = FastLineRendererLineSegmentType.StartCap;
            }
            StartLine(props);
            props.LifeTime.x += animationTime;

            for (; index < path.Count; index++)
            {
                props.Start = path[index];
                if (index == lastIndexMinusOne)
                {
                    if (endCap)
                    {
                        props.LineType = FastLineRendererLineSegmentType.EndCap;
                    }
                    EndLine(props);
                    props.LifeTime.x += animationTime;
                }
                else
                {
                    AppendLine(props);
                    props.LifeTime.x += animationTime;
                }
            }

            path.Clear();
            props.LineJoin = prevJoin;
            props.LineType = prevType;
            props.LifeTime.x = xLifeTime;

            return true;
        }

        /// <summary>
        /// Append a circle to the fast line renderer
        /// </summary>
        /// <param name="props">Properties</param>
        /// <param name="center">Center in world space</param>
        /// <param name="radius">Radius in world units</param>
        /// <param name="numberOfSegments">How many line segments the circle should be composed of. Will be raised to 4 if less than 4</param>
        /// <param name="axis">Axis to rotate around the center to add points. For 2D this is ignored.</param>
        /// <param name="animationTime">The time it takes for each line segment of the spline to animate in</param>
        public void AppendCircle(FastLineRendererProperties props, Vector3 center, float radius, int numberOfSegments, Vector3 axis, float animationTime = 0.0f)
        {
            AppendArc(props, center, radius, 360, 0, numberOfSegments, axis, true, animationTime);
        }

        /// <summary>
        /// Append an arc to the fast line renderer
        /// </summary>
        /// <param name="props">Properties</param>
        /// <param name="center">Center in world space</param>
        /// <param name="radius">Radius in world units</param>
        /// <param name="startAngle">Start angle in degrees (0-360) - for clockwise, make this larger than endAngle</param>
        /// <param name="endAngle">End angle in degrees (0-360) - for clockwise, make this smaller than startAngle</param>
        /// <param name="numberOfSegments">How many line segments the circle should be composed of. Will be raised to 4 if less than 4</param>
        /// <param name="axis">Axis to rotate around the center to add points. For 2D this is ignored.</param>
        /// <param name="close">Whether to close the arc - for a circle this should be true, but false otherwise</param>
        /// <param name="animationTime">The time it takes for each line segment of the spline to animate in</param>
        public void AppendArc(FastLineRendererProperties props, Vector3 center, float radius, float startAngle, float endAngle, int numberOfSegments, Vector3 axis, bool close = false, float animationTime = 0.0f)
        {
            float range = Mathf.Abs(endAngle - startAngle);
            close = close || (range >= 360.0f);
            numberOfSegments = Mathf.Clamp(numberOfSegments, 4, 512) + (close ? 0 : 1);

            float direction = Mathf.Sign(endAngle - startAngle);
            float xLifeTime = props.LifeTime.x;
            float angleIncrement = (range / (float)numberOfSegments) * direction;
            float angle = startAngle;
            int index = 1;
            Vector3 vector = new Vector3(radius, 0.0f, 0.0f);
            Vector3 start;
            Vector4 startLineDir1;
            Vector4 startLineDir2;
            Quaternion q;

            FastLineRendererLineJoin prevJoin = props.LineJoin;
            props.LineJoin = FastLineRendererLineJoin.AttachToPrevious;
            FastLineRendererLineSegmentType prevType = props.LineType;
            props.LineType = FastLineRendererLineSegmentType.Full;

            if (Camera.orthographic)
            {
                axis = Vector3.forward;
            }

            // start off the arc
            q = Quaternion.AngleAxis(angle, axis);
            props.Start = start = center + (q * vector);
            angle += angleIncrement;
            q = Quaternion.AngleAxis(angle, axis);
            props.End = center + (q * vector);
            StartLine(props);

            // save the start line dir so we can attach the end of the circle to the start properly
            startLineDir1 = lineDirs[lineDirs.Count - 1];
            startLineDir2 = lineDirs[lineDirs.Count - 3];

            // loop the arc
            for (; index < numberOfSegments; index++)
            {
                angle += angleIncrement;
                q = Quaternion.AngleAxis(angle, axis);
                props.Start = center + (q * vector);
                AppendLine(props);
                props.LifeTime.x += animationTime;
            }
            if (close)
            {
                props.Start = start;
                EndLine(props);
                lineDirs[lineDirs.Count - 1] = startLineDir1;
                lineDirs[lineDirs.Count - 3] = startLineDir2;
            }

            props.LineJoin = prevJoin;
            props.LineType = prevType;
            props.LifeTime.x = xLifeTime;
        }

        /// <summary>
        /// Append a grid
        /// </summary>
        /// <param name="props">Line properties</param>
        /// <param name="bounds">Grid bounds</param>
        /// <param name="cellSize">Size of grid lines</param>
        /// <param name="fill">Whether to fill the bounds or just do the edges</param>
        /// <param name="animationTime">Animation time</param>
        public void AppendGrid(FastLineRendererProperties props, Bounds bounds, float cellSize, bool fill, float animationTime = 0.0f)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            float gridSizeX = max.x - min.x;
            float gridSizeY = max.y - min.y;
            float gridSizeZ = max.z - min.z;
            float startX = min.x;
            float startY = min.y;
            float startZ = min.z;

            if (fill)
            {
                for (float y = 0; y <= gridSizeY; y += cellSize)
                {
                    // X axis lines
                    for (float z = 0; z <= gridSizeZ; z += cellSize)
                    {
                        props.Start = new Vector3(startX, startY + y, startZ + z);
                        props.End = new Vector3(startX + gridSizeX, startY + y, startZ + z);
                        props.LifeTime.x += animationTime;
                        AddLine(props);
                    }

                    // Z axis lines
                    for (float x = 0; x <= gridSizeX; x += cellSize)
                    {
                        props.Start = new Vector3(startX + x, startY + y, startZ);
                        props.End = new Vector3(startX + x, startY + y, startZ + gridSizeZ);
                        props.LifeTime.x += animationTime;
                        AddLine(props);
                    }
                }

                // Y axis lines
                for (float z = 0; z <= gridSizeZ; z += cellSize)
                {
                    for (float x = 0; x <= gridSizeX; x += cellSize)
                    {
                        props.Start = new Vector3(startX + x, startY, startZ + z);
                        props.End = new Vector3(startX + x, startY + gridSizeY, startZ + z);
                        props.LifeTime.x += animationTime;
                        AddLine(props);
                    }
                }
            }
            else
            {
                // bottom face
                bounds.SetMinMax(min, min + new Vector3(gridSizeX, 0.0f, gridSizeZ));
                AppendGrid(props, bounds, cellSize, true, animationTime);

                // top face
                bounds.SetMinMax(min + new Vector3(0.0f, gridSizeY, 0.0f), max);
                AppendGrid(props, bounds, cellSize, true, animationTime);

                // left face
                bounds.SetMinMax(min, min + new Vector3(0.0f, gridSizeY, gridSizeZ));
                AppendGrid(props, bounds, cellSize, true, animationTime);

                // right face
                bounds.SetMinMax(min + new Vector3(gridSizeX, 0.0f, 0.0f), max);
                AppendGrid(props, bounds, cellSize, true, animationTime);

                // front face
                bounds.SetMinMax(min, min + new Vector3(gridSizeX, gridSizeY, 0.0f));
                AppendGrid(props, bounds, cellSize, true, animationTime);

                // back face
                bounds.SetMinMax(min + new Vector3(0.0f, 0.0f, gridSizeZ), max);
                AppendGrid(props, bounds, cellSize, true, animationTime);
            }
        }

        /// <summary>
        /// Changes the position of a single line segment.
        /// This method does not support join styles.
        /// You must call Apply to make the changes permanent.
        /// Error will happen if index is out of range.
        /// </summary>
        /// <param name="index">Line segment index. Each line segment has it's own start and end position.</param>
        /// <param name="newStart">The new start position of the line segment</param>
        /// <param name="newEnd">The new end position of the line segment</param>
        /// <returns>True if success, false if error</returns>
        public bool ChangePosition(int index, Vector3 newStart, Vector3 newEnd)
        {
            try
            {
                index *= 4;
                int listIndex = index / MaxVerticesPerMesh;
                List<Vector4> lineDirs = lineDirsLists[listIndex];
                List<Vector3> vertices = verticesLists[listIndex];
                float radius = lineDirs[index].w;
                Vector4 dir = new Vector4(newEnd.x - newStart.x, newEnd.y - newStart.y, newEnd.z - newStart.z, radius);
                lineDirs[index] = dir;
                vertices[index++] = newStart;
                lineDirs[index] = dir;
                vertices[index++] = newEnd;
                dir.w = -radius;
                lineDirs[index] = dir;
                vertices[index++] = newStart;
                lineDirs[index] = dir;
                vertices[index] = newEnd;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Apply all line creations
        /// </summary>
        /// <returns>True if success, false if error</returns>
        public bool Apply()
        {
            try
            {
                ApplyListsToMeshes();
                UpdateCanvasRendererMesh();
                return true;
            }
            catch
            {
                Cleanup(true);
                return false;
            }
        }

        /// <summary>
        /// Obslete. Use apply without a parameter. Mesh optimization is no longer needed as of Unity 5.5.
        /// </summary>
        /// <param name="optimize">Optimize</param>
        /// <returns>Success = true, fail = false</returns>
        [Obsolete("Use apply without a parameter. Mesh optimization is no longer needed as of Unity 5.5.")]
        public bool Apply(bool optimize)
        {
            return Apply();
        }

        /// <summary>
        /// Reset everything, remove all lines
        /// </summary>
        public void Reset()
        {
            Cleanup(false);
            InitialLineGroups = defaultInitialGroups;
        }

        /// <summary>
        /// Copy the properties of this fast line renderer to another fast line renderer
        /// </summary>
        /// <param name="other">FastLineRenderer to copy to</param>
        public void CopyTo(FastLineRenderer other)
        {
            other.BoundsScale = BoundsScale;
            other.Camera = Camera;
            other.TintColor = TintColor;
            other.GlowColor = GlowColor;
            other.GlowIntensityMultiplier = GlowIntensityMultiplier;
            other.GlowLengthMultiplier = GlowLengthMultiplier;
            other.GlowWidthMultiplier = GlowWidthMultiplier;
            other.GlowTexture = GlowTexture;
            other.GlowTextureStartCap = GlowTextureStartCap;
            other.GlowTextureEndCap = GlowTextureEndCap;
            other.GlowTextureRoundJoin = GlowTextureRoundJoin;
            other.GlowUVXScale = GlowUVXScale;
            other.GlowUVYScale = GlowUVYScale;
            other.JitterMultiplier = JitterMultiplier;
            other.LineTexture = LineTexture;
            other.LineTextureStartCap = LineTextureStartCap;
            other.LineTextureEndCap = LineTextureEndCap;
            other.LineTextureRoundJoin = LineTextureRoundJoin;
            other.LineUVXScale = LineUVXScale;
            other.LineUVYScale = LineUVYScale;
            other.Material = Material;
            other.MaterialNoGlow = MaterialNoGlow;
            other.SortLayerName = SortLayerName;
            other.SortOrderInLayer = SortOrderInLayer;
            other.Turbulence = Turbulence;
        }

        /// <summary>
        /// Reset and then add this FastLineRenderer to the cache
        /// </summary>
        public void SendToCache()
        {
            if (this != null)
            {
                Reset();
                cache.Add(this);
            }
        }

        /// <summary>
        /// Send this FastLineRenderer back to the cache after a certain time
        /// </summary>
        /// <param name="elapsed">Time to wait before sending to the cache</param>
        public void SendToCacheAfter(TimeSpan elapsed)
        {
            StartCoroutine(SendToCacheAfterCoRoutine(elapsed));
        }

        /// <summary>
        /// Create a new FastLineRenderer or retrieve from cache if available
        /// </summary>
        /// <param name="parent">Parent</param>
        /// <param name="template">Template fast line renderer to use for properties of the new or cached fast line renderer object. Must not be null.
        /// You probably want to create a fast line renderer in you scene view, get it looking how you want, disable it, and then pass that as the template.</param>
        /// <returns>FastLineRenderer from cache or new</returns>
        public static FastLineRenderer CreateWithParent(GameObject parent, FastLineRenderer template)
        {
            FastLineRenderer r = null;
            if (cache.Count == 0)
            {
                GameObject obj = new GameObject();
                obj.name = "FastLineRenderer_" + Guid.NewGuid().ToString("N");
                obj.hideFlags = HideFlags.HideAndDontSave;
                obj.transform.parent = (parent == null ? null : parent.transform);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.rotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
                obj.layer = (parent == null ? obj.layer : parent.layer);
                r = obj.AddComponent<FastLineRenderer>();
                r.Awake();
            }
            else
            {
                foreach (FastLineRenderer rr in cache)
                {
                    r = rr;
                    break;
                }
                cache.Remove(r);
            }
            r.InitialLineGroups = null;
            template.CopyTo(r);
            r.Update();
            r.gameObject.transform.parent = (parent == null ? null : parent.transform);

            return r;
        }
    }
}