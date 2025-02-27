using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(RectTransform))]
[System.Serializable]
public class LayoutMaxSize : LayoutElement
{
    public float maxHeight = -1;
    public float maxWidth = -1;

    public override void CalculateLayoutInputHorizontal()
    {
        base.CalculateLayoutInputHorizontal();
        UpdateMaxSizes();
    }

    public override void CalculateLayoutInputVertical()
    {
        base.CalculateLayoutInputVertical();
        UpdateMaxSizes();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        UpdateMaxSizes();
    }

#if UNITY_EDITOR
    protected override void OnValidate() {
        base.OnValidate();
        UpdateMaxSizes();
    }
#endif

    private void UpdateMaxSizes()
    {
        if (maxHeight != -1)
        {
            if (preferredHeight == -1 && maxHeight < GetComponent<RectTransform>().sizeDelta.y)
            {
                preferredHeight = maxHeight;
            }
            else if (preferredHeight != -1 && transform.childCount > 0)
            {
                bool first = true;
                float biggestY = 0;
                float lowestY = 0;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var childrenTransform = transform.GetChild(i).GetComponent<RectTransform>();
                    if (childrenTransform == null) continue;
                    var childPos = childrenTransform.localPosition;
                    var childSize = childrenTransform.sizeDelta;
                    var childPivot = childrenTransform.pivot;
                    if(first)
                    {
                        biggestY = childPos.y + (childSize.y * (1f - childPivot.y));
                        lowestY = childPos.y - (childSize.y * childPivot.y);
                    }
                    else
                    {
                        biggestY = Mathf.Max(biggestY, childPos.y + (childSize.y * (1f - childPivot.y)));
                        lowestY = Mathf.Min(lowestY, childPos.y - (childSize.y * childPivot.y));
                    }
                    first = false;
                }
                if (first) return;
                var childrenYSize = Mathf.Abs(biggestY - lowestY);
                if(preferredHeight > childrenYSize)
                {
                    preferredHeight = -1;
                }
            }
        }
        if (maxWidth != -1)
        {
            if (preferredWidth == -1 && maxWidth < GetComponent<RectTransform>().sizeDelta.x)
            {
                preferredWidth = maxWidth;
                flexibleWidth = 1;
            }
            else if (preferredWidth != -1 && transform.childCount > 0) {
                bool first = true;
                float biggestX = 0;
                float lowestX = 0;
                for (int i = 0; i < transform.childCount; i++)
                {
                    var childrenTransform = transform.GetChild(i).GetComponent<RectTransform>();
                    if (childrenTransform == null) continue;
                    var childPos = childrenTransform.localPosition;
                    var childSize = childrenTransform.sizeDelta;
                    var childPivot = childrenTransform.pivot;
                    if (first)
                    {
                        biggestX = childPos.x + (childSize.x * (1f - childPivot.x));
                        lowestX = childPos.x - (childSize.x * childPivot.x);
                    }
                    else
                    {
                        biggestX = Mathf.Max(biggestX, childPos.x + (childSize.x * (1f - childPivot.x)));
                        lowestX = Mathf.Min(lowestX, childPos.x - (childSize.x * childPivot.x));
                    }
                    first = false;
                }
                if (first) return;
                var childrenXSize = Mathf.Abs(biggestX - lowestX);
                if (preferredWidth > childrenXSize)
                {
                    preferredWidth = -1;
                }
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(LayoutMaxSize))]
public class LayoutMaxSizeEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        LayoutMaxSize layoutMax = target as LayoutMaxSize;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Ignore Layout");
        layoutMax.ignoreLayout = EditorGUILayout.Toggle(layoutMax.ignoreLayout);
        EditorGUILayout.EndHorizontal();
        if (!layoutMax.ignoreLayout)
        {
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Flexible Width");
            var flexibleWidth = EditorGUILayout.Toggle(layoutMax.flexibleWidth != -1);
            if (flexibleWidth) {
                if (layoutMax.flexibleWidth == -1) layoutMax.flexibleWidth = 0;
                layoutMax.flexibleWidth = EditorGUILayout.FloatField(layoutMax.flexibleWidth);
            } else if (layoutMax.flexibleWidth != -1) {
                layoutMax.flexibleWidth = -1;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Min Width");
            var allowMinWidth = EditorGUILayout.Toggle(layoutMax.minWidth != -1);
            if (allowMinWidth)
            {
                if (layoutMax.minWidth == -1) layoutMax.minWidth = 0;
                layoutMax.minWidth = EditorGUILayout.FloatField(layoutMax.minWidth);
            }
            else if (layoutMax.minWidth != -1)
            {
                layoutMax.minWidth = -1;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Min Height");
            var allowMinHeight = EditorGUILayout.Toggle(layoutMax.minHeight != -1);
            if (allowMinHeight)
            {
                if (layoutMax.minHeight == -1) layoutMax.minHeight = 0;
                layoutMax.minHeight = EditorGUILayout.FloatField(layoutMax.minHeight);
            }
            else if (layoutMax.minHeight != -1)
            {
                layoutMax.minHeight = -1;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Max Width");
            var allowMaxWidth = EditorGUILayout.Toggle(layoutMax.maxWidth != -1);
            if (allowMaxWidth)
            {
                if (layoutMax.maxWidth == -1) layoutMax.maxWidth = Mathf.Max(0, layoutMax.minWidth);
                layoutMax.maxWidth = Mathf.Max(EditorGUILayout.FloatField(layoutMax.maxWidth), layoutMax.minWidth);
            }
            else if(layoutMax.maxWidth != -1)
            {
                layoutMax.maxWidth = -1;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Max Height");
            var allowMaxHeight = EditorGUILayout.Toggle(layoutMax.maxHeight != -1);
            if (allowMaxHeight)
            {
                if (layoutMax.maxHeight == -1) layoutMax.maxHeight = Mathf.Max(0, layoutMax.minHeight);
                layoutMax.maxHeight = Mathf.Max(EditorGUILayout.FloatField(layoutMax.maxHeight), layoutMax.minHeight);
            }
            else if (layoutMax.maxHeight != -1)
            {
                layoutMax.maxHeight = -1;
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif