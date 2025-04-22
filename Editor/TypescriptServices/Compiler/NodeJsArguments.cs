using System.Collections.Generic;

namespace Airship.Editor {
    internal struct NodeJsArguments {
        public int MaxOldSpaceSize { get; set; }
        public bool Inspect { get; set; }

        public string GetCommandString() {
            var args = new List<string>();
            
            if (MaxOldSpaceSize != default) {
                args.Add($"--max-old-space-size={MaxOldSpaceSize}");
            }

            if (Inspect) {
                args.Add("--inspect");
            }
            
            return string.Join(" ", args);
        }
    }
}