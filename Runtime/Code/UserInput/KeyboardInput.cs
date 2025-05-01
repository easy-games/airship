using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class KeyboardInput : IDisposable {
    public event Action<Key> KeyDown;
    public event Action<Key> KeyUp;
    
    private InputActionMap _inputMap = new InputActionMap("keyboard");
    
    public KeyboardInput() {
        // Capture all keys and bind them to the input action map:
        foreach (var keyControl in Keyboard.current.allKeys) {
            if (keyControl == null) continue;
            
            var action = _inputMap.AddAction(keyControl.name);
            action.AddBinding($"<Keyboard>/{keyControl.name}");
        }
        
        _inputMap.actionTriggered += context => {
            if (context.control is not KeyControl keyControl) return;
            switch (context.action.phase) {
                case InputActionPhase.Started:
                    KeyDown?.Invoke(keyControl.keyCode);
                    break;
                case InputActionPhase.Canceled:
                    KeyUp?.Invoke(keyControl.keyCode);
                    break;
            }
        };
        
        _inputMap.Enable();
    }

    public void Dispose() {
        _inputMap.Disable();
    }
}
