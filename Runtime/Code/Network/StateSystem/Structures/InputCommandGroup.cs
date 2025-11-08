using System;
using Code.Player.Character.Net;
using UnityEngine;

namespace Code.Network.StateSystem.Structures {
    public class InputCommandGroup<BaseInput, Diff> where BaseInput : InputCommand where Diff : InputCommandDiff {
        public BaseInput baseInput;
        public Diff[] diffs;
        
        public InputCommandGroup() {}
        
        public InputCommandGroup(BaseInput[] inputs) {
            if (inputs == null | inputs.Length == 0) {
                throw new ApplicationException("No inputs provided to input group.");
            }
            this.baseInput = inputs[0];
            if (inputs.Length == 1) {
                return;
            }
            
            diffs = new Diff[inputs.Length - 1];
            var lastInput = this.baseInput;
            for (var i = 1; i < inputs.Length; i++) {
                diffs[i - 1] = lastInput.CreateDiff(inputs[i]) as Diff;
                lastInput = inputs[i];
            }
        }

        public BaseInput[] RetrieveInputs() {
            if (baseInput == null) return null;
            BaseInput[] inputs = new BaseInput[diffs == null ? 1 : diffs.Length + 1];

            inputs[0] = baseInput;
            var lastInput = baseInput;
            for (var i = 1; i < inputs.Length; i++) {
                inputs[i] = lastInput.ApplyDiff(diffs[i - 1]) as BaseInput;
                lastInput = inputs[i];
            }

            Debug.Log($"Retrieved {inputs.Length} inputs from group. Diff size was {diffs?.Length}");
            return inputs;
        }
    }
}