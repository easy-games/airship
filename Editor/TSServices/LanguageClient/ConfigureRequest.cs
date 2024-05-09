using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Airship.Editor.LanguageClient {

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum WatchFileKind {
        FixedPollingInterval,
        PriorityPollingInterval,
        DynamicPriorityPolling,
        FixedChunkSizePolling,
        UseFsEvents,
        UseFsEventsOnParentDirectory,
    }
    
    [JsonConverter(typeof(StringEnumConverter))]
    internal enum WatchDirectoryKind  {
        UseFsEvents,
        FixedPollingInterval,
        DynamicPriorityPolling,
        FixedChunkSizePolling,
    }

    [JsonConverter(typeof(StringEnumConverter))]
    internal enum PollingWatchKind {
        FixedInterval,
        PriorityInterval,
        DynamicPriority,
        FixedChunkSize,
    }

    internal struct TsFileRequestArgs {
        public string file; // test
        [CanBeNull] public string projectFileName;
    }

    internal class TsWatchOptions {
        public WatchFileKind? watchFile;
        public WatchDirectoryKind? watchDirectory;
        public PollingWatchKind? fallbackPolling;
        public bool? synchronousWatchDirectory;
        public string[] excludeDirectories;
        public string[] excludeFiles;
    }
    
    internal struct TsServerUserPreferences {
    }

    internal struct TsServerConfigureArguments {
        [CanBeNull] public string hostInfo;
        [CanBeNull] public string file;
        [CanBeNull] public TsWatchOptions watchOptions;
        public TsServerUserPreferences preferences;
    }
}