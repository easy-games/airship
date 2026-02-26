using System;
using System.Collections.Generic;
using Code.Managers;
using UnityEngine;


[LuauAPI]
public class CanvasDistanceCondition : MonoBehaviour {
    private HashSet<int> disablers = new HashSet<int>();
    private int disablerId = 0;
    
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

    private void OnEnable() {
        CanvasDistanceManager.Instance.Register(this);
    }

    /// <summary>
    /// Adds a disabler to this distance condition. While any disablers exist this condition won't run.
    /// </summary>
    /// <returns>
    /// Id of disabler that can be passed to RemoveDisabler
    /// </returns>
    public int AddDisabler() {
        disablerId++;
        disablers.Add(disablerId);
        return disablerId;
    }

    public void RemoveDisabler(int id) {
        disablers.Remove(id);
    }
    
    public bool IsConditionDisabled() {
        return this.disablers.Count > 0;
    }
}