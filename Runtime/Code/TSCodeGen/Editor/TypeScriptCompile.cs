using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;
using Debug = UnityEngine.Debug;

namespace Code.TSCodeGen.Editor
{
    static class ToolbarStyles
    {
        public static readonly GUIStyle CommandButtonStyle;
        public static readonly GUIStyle CommandButtonDisabledStyle;

        static ToolbarStyles()
        {
            CommandButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                imagePosition = ImagePosition.ImageLeft,
            };
        }
    }

    [InitializeOnLoad]
    public static class CompileTypeScriptButton
    {
        private static bool _compiling = false;
        private static readonly GUIContent BuildButtonContent;
        private static readonly GUIContent CompileInProgressContent;
        private static string authToken;

        static CompileTypeScriptButton()
        {
            ToolbarExtender.RightToolbarGUI.Add(OnToolbarGUI);

            BuildButtonContent = new GUIContent
            {
                text = "  Build Game",
                image = LoadImage("Assets/Runtime/Code/TSCodeGen/Editor/build-ts.png"),
            };
            CompileInProgressContent = new GUIContent
            {
                text = "  Building...",
                image = LoadImage("Assets/Runtime/Code/TSCodeGen/Editor/build-ts.png"),
            };
        }

        private static Texture2D LoadImage(string filepath)
        {
            var texture = new Texture2D(1, 1);
            texture.LoadImage(System.IO.File.ReadAllBytes(filepath));
            texture.Apply();
            return texture;
        }

        private static void OnToolbarGUI()
        {
            if (_compiling)
            {
                GUILayout.Label(CompileInProgressContent, new GUIStyle(EditorStyles.toolbarButton)
                {
                    imagePosition = ImagePosition.ImageLeft,
                });
            } else
            {
                if (GUILayout.Button(BuildButtonContent, ToolbarStyles.CommandButtonStyle))
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Debug.LogWarning("Cannot build while the game is running");
                        return;
                    }
                    CompileTypeScript();
                }
            }
            GUILayout.FlexibleSpace();
        }

        private static void CompileTypeScript()
        {
            var gameDirExists = Directory.Exists("Assets/Game");
            if (!gameDirExists)
            {
                UnityEngine.Debug.LogError("No Game directory found within Assets");
                return;
            }

            var dirs = Directory.GetDirectories("Assets/Game");
            if (dirs.Length == 0)
            {
                UnityEngine.Debug.LogError("No game directory found within Assets/Game");
                return;
            }

            var gameDir = dirs[0];
            var tsDir = Path.Join(gameDir, "Typescript~");
            var tsDirExists = Directory.Exists(tsDir);
            if (!tsDirExists)
            {
                UnityEngine.Debug.LogError("No Typescript~ directory found");
                return;
            }

            // var requiresInstall = !Directory.Exists(Path.Join(tsDir, "node_modules"));
            var requiresInstall = true;

            _compiling = true;
            authToken = AuthConfig.instance.githubAccessToken;

            UnityEngine.Debug.Log("Compiling TS...");
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    if (requiresInstall)
                    {
                        UnityEngine.Debug.Log("Installing NPM dependencies...");
                        var success = RunNpmInstall(tsDir);
                        if (!success)
                        {
                            UnityEngine.Debug.LogWarning("Failed to install NPM dependencies");
                            _compiling = false;
                            return;
                        }
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
            if (string.IsNullOrEmpty(authToken))
            {
                Debug.LogError("Missing Github Access Token! Add in EasyGG/Configuration");
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
            procStartInfo.EnvironmentVariables.Add("EASY_AUTH_TOKEN", authToken);
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
            procStartInfo.EnvironmentVariables.Add("EASY_AUTH_TOKEN", authToken);
#endif

            var proc = new Process();
            proc.StartInfo = procStartInfo;
            proc.StartInfo.Environment["EASY_AUTH_TOKEN"] = authToken;
            Debug.Log("using auth token: " + authToken);

            proc.OutputDataReceived += (sender, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.Log(data.Data);
            };
            proc.ErrorDataReceived += (sender, data) =>
            {
                if (data.Data == null) return;
                UnityEngine.Debug.LogWarning(data.Data);
            };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            // proc.StandardInput.WriteLine("whoami");
            proc.WaitForExit();

            if (proc.ExitCode != 0)
            {
                UnityEngine.Debug.LogWarning($"Exit code is: {proc.ExitCode}");
            //     UnityEngine.Debug.LogWarning("Error: " + proc.StandardError.ReadToEnd());
            } else
            {
            //     UnityEngine.Debug.Log("Output: " + proc.StandardOutput.ReadToEnd());
            }


            return proc.ExitCode == 0;
        }
    }
}
