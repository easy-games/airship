using System;
using Code.Managers;
using UnityEngine;


[LuauAPI]
public class CanvasDistanceCondition : MonoBehaviour {
    public float maxDistance {
        get => _maxDistance;
        set {
            _maxDistance = value;
            maxDistanceSqrd = Mathf.Pow(value, 2);
        }
    }
    [SerializeField]
    private float _maxDistance = 50.0f;
    [NonSerialized]
    public float maxDistanceSqrd = 2500.0f;

    private void Start() {
        CanvasDistanceManager.Instance.Register(this);
    }
}