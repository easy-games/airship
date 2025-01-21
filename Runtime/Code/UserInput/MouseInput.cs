using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

public class MouseInput : IDisposable {
    public event Action<MouseButton, bool> OnButton;
    public event Action<Vector2> OnMove;
    public event Action<float> OnScroll;

    private readonly InputActionMap _inputMap = new InputActionMap("mouse");

    public MouseInput() {
        // The mouse controls we care about capturing:
        InputControl[] controls = {
            Mouse.current.leftButton,
            Mouse.current.middleButton,
            Mouse.current.rightButton,
            Mouse.current.forwardButton,
            Mouse.current.backButton,
            Mouse.current.position,
            Mouse.current.scroll,
        };
        
        // Bind controls to the action map:
        foreach (var control in controls) {
            var action = _inputMap.AddAction(control.name);
            action.AddBinding($"<Mouse>/{control.name}");
        }
        
        _inputMap.actionTriggered += context => {
            switch (context.control) {
                case ButtonControl buttonControl: {
                    // Only care about Started and Canceled phases:
                    if (context.action.phase is InputActionPhase.Started or InputActionPhase.Canceled) {
                        var down = context.action.phase == InputActionPhase.Started;
                        if (context.control == Mouse.current.leftButton) {
                            OnButton?.Invoke(MouseButton.Left, down);
                        } else if (context.control == Mouse.current.rightButton) {
                            OnButton?.Invoke(MouseButton.Right, down);
                        } else if (context.control == Mouse.current.middleButton) {
                            OnButton?.Invoke(MouseButton.Middle, down);
                        } else if (context.control == Mouse.current.forwardButton) {
                            OnButton?.Invoke(MouseButton.Forward, down);
                        } else if (context.control == Mouse.current.backButton) {
                            OnButton?.Invoke(MouseButton.Back, down);
                        }
                    }

                    break;
                }
                case Vector2Control v2Control when context.control == Mouse.current.position: {
                    OnMove?.Invoke(v2Control.value);
                    break;
                }
                case DeltaControl deltaControl when context.control == Mouse.current.scroll: {
                    OnScroll?.Invoke(deltaControl.value.y);
                    break;
                }
            }
        };
        
        _inputMap.Enable();
    }

    public void Dispose() {
        _inputMap.Disable();
    }
}
