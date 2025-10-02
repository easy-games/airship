using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

[LuauAPI]
public class InputControlAPI : BaseLuaAPIClass {
    public override Type GetAPIType() {
        return typeof(InputControl);
    }

    public override Type[] GetDescendantTypes() {
        return new Type[] {
            typeof(UnityEngine.InputSystem.Controls.AxisControl),
            typeof(UnityEngine.InputSystem.Controls.ButtonControl),
            typeof(UnityEngine.InputSystem.Controls.DeltaControl),
            typeof(UnityEngine.InputSystem.Controls.DoubleControl),
            typeof(UnityEngine.InputSystem.Controls.IntegerControl),
            typeof(UnityEngine.InputSystem.Controls.DpadControl),
            typeof(UnityEngine.InputSystem.Controls.KeyControl),
            typeof(UnityEngine.InputSystem.Controls.QuaternionControl),
            typeof(UnityEngine.InputSystem.Controls.StickControl),
            typeof(UnityEngine.InputSystem.Controls.TouchControl),
            typeof(UnityEngine.InputSystem.Controls.Vector2Control),
            typeof(UnityEngine.InputSystem.Controls.Vector3Control),
            typeof(UnityEngine.InputSystem.Controls.AnyKeyControl),
            typeof(UnityEngine.InputSystem.Controls.DiscreteButtonControl),
            typeof(UnityEngine.InputSystem.Controls.TouchPhaseControl),
            typeof(UnityEngine.InputSystem.Controls.TouchPressControl),
        };
    }
}