using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Airship.Editor.LanguageClient {
    internal class LspResponse {
        public int Seq;
        public string Command;
        public string Type;
        [JsonProperty("request_seq")]
        public string RequestSeq;
        public bool Success;

        public override string ToString() {
            return JsonConvert.SerializeObject(this, Formatting.None);
        }
    }
    
    internal class LspRequest<TPayload> {
        public int Seq;
        public string Command;
        public string Type = "request";
        public TPayload Arguments;

        
        /// <summary>
        /// Will convert the request into a JSON string for a language server
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            var resolver = new DefaultContractResolver() {
                NamingStrategy = new CamelCaseNamingStrategy()
            };
            
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings() {
                Formatting = Formatting.None,
                ContractResolver = resolver,
            });
        }
    }

    internal static class TsServerRequests {
        public static int seqIdx = 0;
        
        public static LspRequest<TsServerConfigureArguments> ConfigureRequest(TsServerConfigureArguments arguments) {
            return new LspRequest<TsServerConfigureArguments>() {
                Seq = seqIdx++,
                Command = "configure",
                Arguments = arguments
            };
        }

        public static LspRequest<QuickInfoArguments> QuickInfo(QuickInfoArguments args) {
            return new LspRequest<QuickInfoArguments>() {
                Seq = seqIdx++,
                Command = "quickinfo",
                Arguments = args,
            };
        }
    }


    internal struct QuickInfoArguments {
    }

    internal class TypescriptLanguageClient {
        public string WorkingProcessWorkingDirectory { get; }
        public Process Process { get; private set; }

        public Dictionary<LspRequest<object>, LspResponse> responseStack;
        
        public TypescriptLanguageClient(string processWorkingDirectory) {
            WorkingProcessWorkingDirectory = processWorkingDirectory;
        }

        private LspResponse ReadResponse() {
            Process.StandardOutput.ReadLine(); // content-length (not needed)
            Process.StandardOutput.ReadLine(); // random newline (not needed)
            
            var resolver = new DefaultContractResolver() {
                NamingStrategy = new CamelCaseNamingStrategy()
            };

            var value = Process.StandardOutput.ReadLine();
            return JsonConvert.DeserializeObject<LspResponse>(value, new JsonSerializerSettings() {
                Formatting = Formatting.None,
                ContractResolver = resolver,
                NullValueHandling = NullValueHandling.Ignore,
            }); // lol ??
        }

        public void SendRequest<T>(LspRequest<T> request) {
            Debug.Log($"Sending {request}");
            Process.StandardInput.WriteLine(request.ToString());
            // return ReadResponse();
        }

        public Process Start() {
            var command = "./node_modules/typescript/bin/tsserver";
#if UNITY_EDITOR_OSX
            command = $"-c \"path+=/usr/local/bin && node {command}\"";

            var procStartInfo = new ProcessStartInfo( "/bin/zsh", $"{command}")
            {
                RedirectStandardOutput = displayOutput,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = dir,
                CreateNoWindow = true,
                LoadUserProfile = true,
                Environment = {
                    { "FORCE_COLOR", "0" }
                }
            };
#else
            var procStartInfo = new ProcessStartInfo("node.exe", $"{command}")
            {
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                WorkingDirectory = WorkingProcessWorkingDirectory,
                CreateNoWindow = true,
                LoadUserProfile = true,
            };
#endif
            var proc = new Process();
            proc.StartInfo = procStartInfo;

            proc.Start();
            Process = proc;
            
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            
            proc.OutputDataReceived += (sender, data) => {
                if (data.Data == null) return;
                if (data.Data == "") return;
                if (data.Data.StartsWith("Content-Length")) return;
                
                //var response = JsonConvert.DeserializeObject<LspResponse>(data.Data);
                Debug.Log($"[LSP] Received: {data.Data}");
            };
            
            proc.ErrorDataReceived += (sender, args) => {
                Debug.LogError(args.Data); // lol
            };
            
            // LspResponse response = ReadResponse();
            return proc;
        }
    }
}