using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using CsToTs.TypeScript;
using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;
using Debug = UnityEngine.Debug;

namespace Airship.Editor
{
    internal static class ToolbarStyles
    {
        public static readonly GUIStyle CommandButtonStyle;
        public static readonly GUIStyle CommandButtonDisabledStyle;

        static ToolbarStyles()
        {
            CommandButtonStyle = new GUIStyle("Command") {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                imagePosition = ImagePosition.ImageAbove,
                fontStyle = FontStyle.Bold,
                fixedWidth = 130,
            };
        }
    }

    [InitializeOnLoad]
    public static class CompileTypeScriptButton
    {
        private static bool _compiling;
        private static readonly GUIContent BuildButtonContent;
        private static readonly GUIContent CompileInProgressContent;
        private static string _authToken;

        private const string BuildIcon = "Packages/gg.easy.airship/Editor/TSCodeGen/Editor/build-ts.png";

        [MenuItem("Airship/♻️ Full Script Rebuild", priority = 202)]
        public static void FullRebuild()
        {
            var tsDir = TypeScriptDirFinder.FindTypeScriptDirectory();
            if (tsDir == null)
            {
                UnityEngine.Debug.LogError("No Typescript~ directory found");
                return;
            }

            var outPath = Path.Join(tsDir, "out");
            if (Directory.Exists(outPath))
            {
                Debug.Log("Deleting out folder...");
                Directory.Delete(outPath, true);
            }
            CompileTypeScript();
        }

        static CompileTypeScriptButton()
        {
            ToolbarExtender.RightToolbarGUI.Add(OnToolbarGUI);

            BuildButtonContent = new GUIContent
            {
                text = "  Build Game",
                image = LoadImage(BuildIcon),
            };
            CompileInProgressContent = new GUIContent
            {
                text = "  Building...",
                image = LoadImage(BuildIcon),
            };
        }

        private static Texture2D LoadImage(string filepath)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(filepath);
            return texture;
        }

        private static void OnToolbarGUI()
        {
            if (Application.isPlaying) return;

            if (_compiling) {
                GUILayout.Button(new GUIContent("Building...", "Airship scripts are being built..."), ToolbarStyles.CommandButtonStyle);
            } else {
                if (GUILayout.Button(new GUIContent("Compile Scripts", "Builds all Airship scripts."), ToolbarStyles.CommandButtonStyle)) {
                    CompileTypeScript();
                }
            }
            GUILayout.FlexibleSpace();
        }

        private static void CompileTypeScript()
        {
            var tsDir = TypeScriptDirFinder.FindTypeScriptDirectory();
            if (tsDir == null)
            {
                UnityEngine.Debug.LogError("No Typescript~ directory found");
                return;
            }

            UnityEngine.Debug.Log($"TypeScript directory found: {tsDir}");

            _compiling = true;
            _authToken = AuthConfig.instance.githubAccessToken;

            UnityEngine.Debug.Log("Compiling TS...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    UnityEngine.Debug.Log("Installing NPM dependencies...");
                    var success = RunNpmInstall(tsDir);
                    if (!success)
                    {
                        UnityEngine.Debug.LogWarning("Failed to install NPM dependencies");
                        _compiling = false;
                        return;
                    }

                    var successfulBuild = RunNpmBuild(tsDir);
                    _compiling = false;
                    if (successfulBuild)
                    {
                        UnityEngine.Debug.Log("<color=#77f777><b>Build game succeeded</b></color>");
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning("<color=red><b>Build game failed</b></color>");
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            });
        }

        private static bool RunNpmInstall(string dir)
        {
            return RunNpmCommand(dir, "install");
        }

        private static bool RunNpmBuild(string dir)
        {
            return RunNpmCommand(dir, "run build");
        }

        private static bool RunNpmCommand(string dir, string command)
        {
            if (string.IsNullOrEmpty(_authToken))
            {
                UnityEngine.Debug.LogError("Missing Github Access Token! Add in EasyGG/Configuration");
                return false;
            }

#if UNITY_EDITOR_OSX
            command = $"-c \"path+=/usr/local/bin && npm {command}\"";
            // command = "-c \"whoami && ls /usr/local/bin\"";
            // command = "/usr/local/bin";
            // command = "-c \"alias node=\"/usr/local/bin/node\" && /usr/local/bin/npm run build\"";
            // command = "-c \"scripts/build.sh\"";
            var procStartInfo = new ProcessStartInfo( "/bin/zsh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                // RedirectStandardInput = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = false,
                LoadUserProfile = true,
                Arguments = command,
            };
#else
            var procStartInfo = new ProcessStartInfo("cmd.exe", $"/K npm {command}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = true,
                LoadUserProfile = true,
            };
#endif
            if (!procStartInfo.EnvironmentVariables.ContainsKey("EASY_AUTH_TOKEN"))
            {
                procStartInfo.EnvironmentVariables.Add("EASY_AUTH_TOKEN", _authToken);
            }

            var proc = new Process();
            proc.StartInfo = procStartInfo;
            if (!proc.StartInfo.Environment.ContainsKey("EASY_AUTH_TOKEN"))
            {
                proc.StartInfo.Environment["EASY_AUTH_TOKEN"] = _authToken;
            }
            UnityEngine.Debug.Log("using auth token: " + _authToken);

            proc.OutputDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.Log(data.Data);
            };
            proc.ErrorDataReceived += (_, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.LogWarning(data.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                UnityEngine.Debug.LogWarning($"Exit code is: {proc.ExitCode}");
            }

            return proc.ExitCode == 0;
        }
    }
}
