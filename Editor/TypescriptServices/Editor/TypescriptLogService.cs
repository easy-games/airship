using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Code.State;
using Editor.Util;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Airship.Editor {
    public enum TypescriptLogLevel {
        Information,
        Warning,
        Error,
    }
    
    public static class TypescriptLogService {
        public static bool Enabled {
            get {
                return EditorPrefs.GetBool("AirshipTypescriptLoggingEnabled", true);
            }
            set {
                if (value && !started) {
                    StartLogging();
                }
                else if (!value && started) {
                    StopLogging();
                }
                
                EditorPrefs.SetBool("AirshipTypescriptLoggingEnabled", value);
            }
        }
        
        internal static string LogFolder {
            get {
                string logDir = Path.GetDirectoryName(Application.consoleLogPath);
                string[] s = Application.dataPath.Split('/');
                string projectName = s[^2];
                return Path.Join(logDir, projectName);
            }
        }

        internal static string LogFilePath => Path.Combine(LogFolder, $"Typescript.log");
        internal static string LogFilePrevPath => Path.Combine(LogFolder, $"Typescript-prev.log");
        internal static bool HasPrevLog => File.Exists(LogFilePrevPath);

        [MenuItem("Airship/TypeScript/Open Log File...", priority = 1000000)]
        public static void OpenLogFile() {
            TypescriptProjectsService.OpenFileInEditor(LogFilePath);
        }
        
        public static void OpenPrevLogFile() {
            TypescriptProjectsService.OpenFileInEditor(LogFilePrevPath);
        }

        private static string logPath;
        private static string prevLogPath;
        private static StreamWriter writer;
        private static readonly ConcurrentQueue<string> logQueue = new();

        private static Task writeTask;
        private static bool started;

        private static Stopwatch timer;
        
        private static async Task ProcessQueue() {
            while (started) {
                while (logQueue.TryDequeue(out string msg)) {
                    try {
                        await writer.WriteLineAsync(msg);
                    } catch (Exception e) {
                        Debug.LogError("TypescriptLogger write failed: " + e);
                    }
                }
                
                await Task.Delay(100); // tune as needed
            }
        }

        internal static void StartCompileStopWatch() {
            timer = new Stopwatch();
            timer.Start();
        }

        internal static void StopCompileStopWatch() {
            timer.Stop();
            Log(TypescriptLogLevel.Information, $"Compilation took {timer.Elapsed.Milliseconds} ms.");
        }

        private static void WriteLog(string message) {
            logQueue.Enqueue(message);
        }
        
        internal static void Log(TypescriptLogLevel level, string message) {
            var logLevelText = level switch {
                TypescriptLogLevel.Error => "ERR",
                TypescriptLogLevel.Warning => "WRN",
                TypescriptLogLevel.Information => "INF",
                _ => throw new NotImplementedException(),
            };
            
            string timeStamped = $"[{DateTime.Now:HH:mm:ss}] [{logLevelText}] {message}";
            WriteLog(timeStamped);
        }
        
        internal static void LogInfo(string message) {
            Log(TypescriptLogLevel.Information, message);
        }

        internal static void LogWarning(string message) {
            Log(TypescriptLogLevel.Warning, message);
        }

        internal static void LogEvent(CompiledFileEvent compiledFileEvent) {
            // var project = TypescriptProjectsService.Project;
            // var relative = PosixPath.Join("Assets", Path.GetRelativePath(project.Directory, compiledFileEvent.fileName));
            //
            // Log(TypescriptLogLevel.Information, 
            //     $"Compiled file at {relative}, output to {project.GetOutputPath(relative)}");
        }
        
        internal static void LogFileDiagnostic(TypescriptFileDiagnosticItem diagnosticItem) {
            var message = $"{diagnosticItem.FileLocation}:{diagnosticItem.LineAndColumn.Line} - {diagnosticItem.Message}";

            if (diagnosticItem.ErrorCode > 0) {
                Log(TypescriptLogLevel.Error, $"TS{diagnosticItem.ErrorCode}: {message}"); 
            }
            else {
                Log(TypescriptLogLevel.Error, $"COMPILER: {message}"); 
            }
        }

        internal static void LogCrash(TypescriptCrashProblemItem crash) {
            Log(TypescriptLogLevel.Error, crash.Message);
            WriteLog("======= TYPESCRIPT CRASHED ======");
            WriteLog(string.Join("\n", crash.StandardError));
            WriteLog("==================================");
        }

        internal static void StartLogging() {
            if (!Directory.Exists(LogFolder)) {
                Directory.CreateDirectory(LogFolder);
            }
            
            if (started) return;
            
            string logDir = Path.GetDirectoryName(Application.consoleLogPath);
            logPath = LogFilePath;
            prevLogPath = LogFilePrevPath; // lol
            
            try {
                if (File.Exists(prevLogPath)) File.Delete(prevLogPath);
                if (File.Exists(logPath)) File.Move(logPath, prevLogPath);
            } catch (Exception e) {
                Debug.LogError("Failed rotating typescript logs: " + e);
            }
            
            if (writer != null) {
                writer.Close();
            }
            
            var isDomainReload = SessionState.GetBool("StartedLogger", false);
            if (isDomainReload) {
                Log(TypescriptLogLevel.Information, "Detected domain reload..."); // lol
            }
            
            writer = new StreamWriter(logPath, append: isDomainReload); // overwrite existing
            writer.AutoFlush = true;
            
            writeTask = Task.Run(ProcessQueue);
            started = true;
            
            SessionState.SetBool("StartedLogger", true);
        }
        
        internal static void StopLogging() {
            started = false;
            if (writer != null) {
                writer.Close();
                writer.Dispose();
            }            
        }
        
        [InitializeOnLoadMethod]
        internal static void OnLoad() {
            StartLogging();
        }
    }
}