using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Editor;
using JetBrains.Annotations;
using NUnit;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

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