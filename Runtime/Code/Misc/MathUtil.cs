using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MathUtil : MonoBehaviour
{
    public static float ClampAngle(float angle, float min, float max) {
        float start = (min + max) * 0.5f - 180;
        float floor = Mathf.FloorToInt((angle - start) / 360) * 360;
        return Mathf.Clamp(angle, min + floor, max + floor);
    }
}
