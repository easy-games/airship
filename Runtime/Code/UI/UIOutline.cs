using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[LuauAPI]
[RequireComponent(typeof(CanvasRenderer))]
public class UIOutline : Graphic
{
    [SerializeField] Texture m_Texture;
    [SerializeField, Range(0f, 500f)] float _outlineWidth = 1f;
    [SerializeField, Range(0f, 500f)] float _cornerRadius = 10f;
    [SerializeField, Range(1, 20)] int _cornerSegments = 18;
    [SerializeField, Range(0f, 1f)] float _mappingBias = 0.5f;
    [SerializeField, Tooltip("This should be enabled for very thin outlines that won't render well on low res monitors.")]
    bool _slightlyThickerCorners = false;
    [SerializeField] bool _fillCenter;

    private readonly Vector3[] _corners = new Vector3[4];
    private readonly List<UIVertex> _verts = new List<UIVertex>();

    public override Texture mainTexture => m_Texture == null ? s_WhiteTexture : m_Texture;

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
        SetMaterialDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var rect = rectTransform.rect;

        // Clamp corner radius so arcs make sense
        var clampedCornerRadius = Mathf.Min(Mathf.Min(rect.width, rect.height) * 0.5f, _cornerRadius);

        // Clamp outline so inner radius doesn't go negative
        var clampedOutlineWidth = Mathf.Min(_outlineWidth, clampedCornerRadius);

        // For INNER stroke:
        // Outer boundary is the rect itself (rounded).
        // Inner boundary is inset by outline width.
        var outerRadius = clampedCornerRadius;
        var innerRadius = clampedCornerRadius - clampedOutlineWidth;

        // Get rect corners
        rectTransform.GetLocalCorners(_corners);

        // Move corners to arc centers (inset by outer radius)
        _corners[0] += new Vector3(outerRadius, outerRadius, 0f);     // bottom-left
        _corners[1] += new Vector3(outerRadius, -outerRadius, 0f);    // top-left
        _corners[2] += new Vector3(-outerRadius, -outerRadius, 0f);   // top-right
        _corners[3] += new Vector3(-outerRadius, outerRadius, 0f);    // bottom-right

        // Calculate dimensions along the OUTER boundary (since that's what we draw first)
        var height = (_corners[1].y - _corners[0].y);
        var width = (_corners[2].x - _corners[1].x);
        var edgeLengths = new[] { height, width, height, width };

        // Use bias between outer and inner radius for UV mapping around the shape
        var mappedRadius = Mathf.Lerp(innerRadius, outerRadius, _mappingBias);
        var circumference = 2f * Mathf.PI * mappedRadius;

        var around = height * 2f + width * 2f + circumference;
        var cornerLength = circumference / 4f;
        var segmentLength = cornerLength / _cornerSegments;

        var vert = new UIVertex { color = color };
        _verts.Clear();

        var u = 0f;

        for (var c = 0; c < 4; c++)
        {
            var origin = _corners[c];

            for (var i = 0; i < _cornerSegments + 1; i++)
            {
                var t = (float)i / _cornerSegments;
                var angle = t * Mathf.PI / 2f + Mathf.PI * 0.5f - Mathf.PI * c * 1.5f;
                var direction = new Vector3(Mathf.Cos(-angle), Mathf.Sin(-angle), 0f);

                // OUTER edge (at rect boundary)
                vert.position = origin + direction * outerRadius;
                vert.uv0 = new Vector2(u, 0f);
                _verts.Add(vert);

                // INNER edge (inset)
                var effectiveOutline = clampedOutlineWidth;
                if (i < _cornerSegments && i > 0 && _slightlyThickerCorners)
                {
                    effectiveOutline = Mathf.Min(outerRadius, 1.2f * effectiveOutline);
                }

                var effectiveInnerRadius = Mathf.Max(0f, outerRadius - effectiveOutline);

                vert.position = origin + direction * effectiveInnerRadius;
                vert.uv0 = new Vector2(u, 1f);
                _verts.Add(vert);

                if (_fillCenter)
                {
                    vert.position = rect.center;
                    vert.uv0 = new Vector2(u, 0f);
                    _verts.Add(vert);
                }

                if (i < _cornerSegments)
                {
                    u += segmentLength / around;
                }
                else
                {
                    u += edgeLengths[c] / around;
                }
            }
        }

        // Close the loop (duplicate first outer+inner)
        vert = _verts[0];
        vert.uv0 = new Vector2(1f, 0f);
        _verts.Add(vert);

        vert = _verts[1];
        vert.uv0 = new Vector2(1f, 1f);
        _verts.Add(vert);

        if (_fillCenter)
        {
            vert = _verts[2];
            vert.uv0 = new Vector2(1f, 1f);
            _verts.Add(vert);
        }

        foreach (var vertex in _verts)
        {
            vh.AddVert(vertex);
        }

        if (_fillCenter)
        {
            for (var v = 0; v < vh.currentVertCount - 3; v += 3)
            {
                vh.AddTriangle(v, v + 1, v + 4);
                vh.AddTriangle(v, v + 4, v + 3);

                vh.AddTriangle(v + 2, v, v + 3);
                vh.AddTriangle(v + 2, v + 3, v + 5);
            }
        }
        else
        {
            for (var v = 0; v < vh.currentVertCount - 2; v += 2)
            {
                vh.AddTriangle(v, v + 1, v + 3);
                vh.AddTriangle(v, v + 3, v + 2);
            }
        }
    }
}
