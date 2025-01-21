using System;
using System.Collections.Generic;
using Luau;
using UnityEngine;
using UnityEngine.Profiling;

public class AirshipComponentUpdater : MonoBehaviour {
    private readonly List<AirshipComponent> _components = new List<AirshipComponent>();
    private readonly List<AirshipComponent> _componentsToRemove = new List<AirshipComponent>();
    
    public void Register(AirshipComponent component) {
        if (component.m_canResume && !component.m_error) {
            _components.Add(component);
        }
    }

    public void Unregister(AirshipComponent component) {
        _components.Remove(component);
    }

    private void UpdateComponent(AirshipComponent component) {
        if (component.m_error) {
            _componentsToRemove.Add(component);
            return;
        }
        
        if (component.m_canResume && !component.m_asyncYield) {
            ThreadDataManager.SetThreadYielded(component.m_thread, false);
            var retValue = LuauCore.CoreInstance.ResumeScript(component.context, component);
            if (retValue != 1) {
                component.m_canResume = false;
                _componentsToRemove.Add(component);
                if (retValue == -1) {
                    component.m_error = true;
                }
            }
        }
    }
    
    public void Update() {
        Profiler.BeginSample("UpdateAllAirshipComponents");
        
        // Update all components:
        foreach (var component in _components) {
            UpdateComponent(component);
        }

        // Remove components when top-level thread is done resuming or errored:
        if (_componentsToRemove.Count > 0) {
            foreach (var component in _componentsToRemove) {
                _components.Remove(component);
            }
            _componentsToRemove.Clear();
        }
        
        Profiler.EndSample();
    }
}
    